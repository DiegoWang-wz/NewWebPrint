using System.Data;
using System.Runtime.Serialization.Json;
using System.Text;
using MudBlazor;
using NewWebPrint.DataDtos;
using Microsoft.JSInterop;
using Sap.Data.Hana;

namespace NewWebPrint.Utilities;

public sealed record PrinterLoadResult(
    bool Success,
    IReadOnlyList<string> Printers,
    string? SelectedPrinter,
    string? DefaultPrinter,
    string? ErrorMessage);

public sealed record PaperSizeParseResult(bool Success, float WidthMm, float HeightMm);

public sealed record PrintSubmitResult(bool Success, string LogContent, string? ErrorMessage);

/// <summary>与本地打印 Agent 通讯（通过 JS 调用 localAgent）。</summary>
public static class PrinterHelper
{
    private const string LocalAgentGetPrinters = "localAgent.getPrinters";
    private const string LocalAgentPrintLayout = "localAgent.printLayout";

    /// <summary>
    /// 获取打印机列表并解析默认选中项（上次使用 &gt; 系统默认 &gt; 第一台）。
    /// 失败时可选弹出 Snackbar 提示。
    /// </summary>
    public static async Task<PrinterLoadResult> LoadPrintersAsync(
        IJSRuntime js,
        LocalStorageHelper storage,
        string lastUsedStorageKey,
        SnackbarHelper? snackbar = null,
        bool showSuccessToast = true,
        bool showErrorToast = true)
    {
        var (success, data, errorMsg) = await GetPrintersAsync(js);
        if (!success || data is null)
        {
            if (showErrorToast && !string.IsNullOrEmpty(errorMsg))
                snackbar?.Show(errorMsg, Severity.Error);
            return new PrinterLoadResult(false, [], null, null, errorMsg);
        }

        var printers = data.installed ?? [];
        var lastUsed = await storage.GetLastUsedPrinterAsync(lastUsedStorageKey);
        var selected = ResolveSelectedPrinter(printers, data.@default, lastUsed);

        if (showSuccessToast)
            snackbar?.Show("已获取打印机列表", Severity.Success);

        return new PrinterLoadResult(true, printers, selected, data.@default, null);
    }

    public static async Task<(bool Success, PrintersResp? Data, string? ErrorMessage)> GetPrintersAsync(IJSRuntime js)
    {
        try
        {
            var data = await js.InvokeAsync<PrintersResp>(LocalAgentGetPrinters);
            return (true, data, null);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("Failed to fetch") || msg.Contains("NetworkError"))
                msg = "无法获取打印机列表。请确认 LocalAgent 代理程序已启动，并在菜单中检查「打印代理 IP」是否填写正确（默认 localhost，端口 5005）。";
            else if (msg.Contains("GET /printers"))
                msg = "无法获取打印机列表。请确认 LocalAgent 代理程序已启动，且「打印代理 IP」设置正确。" + (string.IsNullOrWhiteSpace(msg) ? "" : $"（{msg}）");
            return (false, null, msg);
        }
    }

    public static Task PrintLayoutAsync(IJSRuntime js, PrintLayoutModel layout) =>
        js.InvokeVoidAsync(LocalAgentPrintLayout, layout).AsTask();

    /// <summary>解析纸张尺寸，支持 100*80、100x80 等格式。</summary>
    public static PaperSizeParseResult TryParsePaperSize(string? paper)
    {
        try
        {
            var parts = (paper ?? "").Split(
                ['*', 'x', 'X'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2) return new(false, 0, 0);

            var w = float.Parse(parts[0]);
            var h = float.Parse(parts[1]);
            if (w <= 0 || h <= 0) return new(false, 0, 0);

            return new(true, w, h);
        }
        catch
        {
            return new(false, 0, 0);
        }
    }

    /// <summary>解析纸张尺寸，失败时可选弹出 Snackbar。</summary>
    public static PaperSizeParseResult ParsePaperSize(string? paper, SnackbarHelper? snackbar = null)
    {
        var result = TryParsePaperSize(paper);
        if (!result.Success)
            snackbar?.Show("纸张尺寸格式错误（如 100*80）", Severity.Error);
        return result;
    }

    public static bool TryParsePrintRange(string? startText, string? endText, out int start, out int end, SnackbarHelper? snackbar = null)
    {
        start = end = 0;
        if (int.TryParse(startText?.Trim(), out start) &&
            int.TryParse(endText?.Trim(), out end) &&
            start <= end)
            return true;

        snackbar?.Show("请填写有效的打印开始、打印结束序号（整数，且开始≤结束）", Severity.Warning);
        return false;
    }

    public static string ImageFileToBase64(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return string.Empty;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };

            var base64 = Convert.ToBase64String(File.ReadAllBytes(filePath));
            return $"data:{mime};base64,{base64}";
        }
        catch
        {
            return string.Empty;
        }
    }

    public static Task SaveLastUsedPrinterAsync(LocalStorageHelper storage, string? printerName, string storageKey) =>
        storage.SaveLastUsedPrinterAsync(printerName, storageKey);

    /// <summary>兼容旧页面：通过 JS localStorage 读取上次打印机。</summary>
    public static async Task<string?> GetLastUsedPrinterAsync(IJSRuntime js, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", storageKey.Trim());
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>兼容旧页面：通过 JS localStorage 保存上次打印机。</summary>
    public static async Task SaveLastUsedPrinterAsync(IJSRuntime js, string? printerName, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        try
        {
            if (string.IsNullOrWhiteSpace(printerName))
                await js.InvokeVoidAsync("localStorage.removeItem", storageKey.Trim());
            else
                await js.InvokeVoidAsync("localStorage.setItem", storageKey.Trim(), printerName.Trim());
        }
        catch
        {
            // ignore
        }
    }

    public static bool RequireSelectedPrinter(string? printer, SnackbarHelper? snackbar = null)
    {
        if (!string.IsNullOrWhiteSpace(printer)) return true;
        snackbar?.Show("请先选择打印机", Severity.Warning);
        return false;
    }

    public static bool TryParsePrintCount(string? text, out int count, SnackbarHelper? snackbar = null)
    {
        if (int.TryParse(text, out count) && count >= 1) return true;
        count = 0;
        snackbar?.Show("打印数量无效", Severity.Error);
        return false;
    }

    /// <summary>
    /// 提交打印布局：写日志、调用 LocalAgent、保存上次打印机、Snackbar 提示。
    /// </summary>
    public static async Task<PrintSubmitResult> SubmitPrintAsync(
        IJSRuntime js,
        LocalStorageHelper storage,
        PrintLayoutModel layout,
        string lastUsedStorageKey,
        string logContent,
        string? startLogMessage = null,
        SnackbarHelper? snackbar = null,
        Action<bool>? setBusy = null,
        Func<Task>? notifyStateChanged = null)
    {
        setBusy?.Invoke(true);
        var log = logContent;
        if (!string.IsNullOrEmpty(startLogMessage))
            log = OperationLogHelper.Append(log, startLogMessage);

        try
        {
            await PrintLayoutAsync(js, layout);
            await SaveLastUsedPrinterAsync(storage, layout.PrinterName, lastUsedStorageKey);
            log = OperationLogHelper.Append(log, "打印提交成功。");
            snackbar?.Show("打印提交成功", Severity.Success);
            return new PrintSubmitResult(true, log, null);
        }
        catch (Exception ex)
        {
            log = OperationLogHelper.Append(log, $"异常：{ex.Message}");
            snackbar?.Show($"异常：{ex.Message}", Severity.Error);
            return new PrintSubmitResult(false, log, ex.Message);
        }
        finally
        {
            setBusy?.Invoke(false);
            if (notifyStateChanged is not null)
                await notifyStateChanged();
        }
    }

    private static string? ResolveSelectedPrinter(
        IReadOnlyList<string> printers,
        string? defaultPrinter,
        string? lastUsed)
    {
        if (!string.IsNullOrEmpty(lastUsed) && printers.Contains(lastUsed))
            return lastUsed;
        if (!string.IsNullOrWhiteSpace(defaultPrinter) && printers.Contains(defaultPrinter))
            return defaultPrinter;
        return printers.FirstOrDefault();
    }

    public static string GetDaysDifference(string dateString)
    {
        if (DateTime.TryParse(dateString, out var parsedDate))
        {
            var difference = DateTime.Now - parsedDate;
            return difference.TotalDays.ToString("0.0");
        }
        return "";
    }

    public static string ClearFrontZreo(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return "0";

        var result = str.TrimStart('0');
        return string.IsNullOrEmpty(result) ? "0" : result;
    }

    public static DataTable ConvertDataReaderToDataTable(HanaDataReader dataReader)
    {
        var datatable = new DataTable();

        try
        {
            for (var i = 0; i < dataReader.FieldCount; i++)
            {
                var myDataColumn = new DataColumn
                {
                    DataType = dataReader.GetFieldType(i),
                    ColumnName = dataReader.GetName(i)
                };
                datatable.Columns.Add(myDataColumn);
            }

            while (dataReader.Read())
            {
                var myDataRow = datatable.NewRow();
                for (var i = 0; i < dataReader.FieldCount; i++)
                    myDataRow[i] = dataReader[i].ToString();

                datatable.Rows.Add(myDataRow);
            }

            dataReader.Close();
            return datatable;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    public static T JsonToObject<T>(string jsonText)
    {
        var s = new DataContractJsonSerializer(typeof(T));
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonText));
        var obj = (T)s.ReadObject(ms)!;
        ms.Dispose();
        return obj;
    }
}
