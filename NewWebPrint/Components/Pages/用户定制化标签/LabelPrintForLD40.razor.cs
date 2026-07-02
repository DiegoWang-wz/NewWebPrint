using System.Globalization;
using Microsoft.AspNetCore.Components;
using NewWebPrint.DataDtos;
using NewWebPrint.Models.Api;
using NewWebPrint.Services.Ld40Binding;
using NewWebPrint.Services.SapEwm;
using NewWebPrint.Utilities;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace NewWebPrint.Components.Pages.用户定制化标签;

public partial class LabelPrintForLD40
{
    const string PrinterStorageKey = "LabelPrintForLD40_printer";
    const int MaxBindPerBox = 24;
    const int BarcodeLength = 15;
    const int DefaultFontSizePt = 13;

    [Inject] ISapEwmService SapEwmService { get; set; } = null!;
    [Inject] SnackbarHelper SnackbarHelper { get; set; } = null!;
    [Inject] LocalStorageHelper LocalStorage { get; set; } = null!;
    [Inject] ILd40BindingService Ld40Binding { get; set; } = null!;
    [Inject] LocalAgentPrinterBootstrap PrinterBootstrap { get; set; } = null!;

    List<string> Printers { get; set; } = new();
    string? DefaultPrinter { get; set; }
    string? SelectedPrinter { get; set; }

    SapEwmDynamicPayload? _orderData;
    bool _isSearching;
    bool _isPrinting;
    string _saveState = "人工打印";

    string? _seachBoxNo;
    string? _seachBarcode;

    public string WorkNo { get; set; } = string.Empty;
    public string MaterialNo { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public string BarcodeList { get; set; } = string.Empty;
    public string BoxText { get; set; } = "BS24-01340\nMOT-USS-PMDC-JC ALL-IN-009565-01 REV1\nARTICLENO.:000.008.182\nQTY:24pcs\nProduction Date:2024.5.22";

    public string BindNumber { get; set; } = "0";
    public string BindSum { get; set; } = string.Empty;
    public string BoxNum { get; set; } = string.Empty;
    public string Worker { get; set; } = string.Empty;
    public string PrintMessages { get; set; } = string.Empty;

    public string Seach_BoxNo
    {
        get => _seachBoxNo ?? string.Empty;
        set
        {
            _seachBoxNo = value;
            if (!string.IsNullOrWhiteSpace(value))
                _seachBarcode = string.Empty;
        }
    }

    public string Seach_Barcode
    {
        get => _seachBarcode ?? string.Empty;
        set
        {
            _seachBarcode = value;
            if (!string.IsNullOrWhiteSpace(value))
                _seachBoxNo = string.Empty;
        }
    }

    public string Search_value { get; set; } = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await PrinterBootstrap.TryLoadOnFirstRenderAsync(
            firstRender,
            PrinterStorageKey,
            ApplyPrinterLoadResult,
            () => InvokeAsync(StateHasChanged));
    }

    void ApplyPrinterLoadResult(PrinterLoadResult result)
    {
        if (!result.Success) return;
        Printers = result.Printers.ToList();
        DefaultPrinter = result.DefaultPrinter;
        SelectedPrinter = result.SelectedPrinter;
    }

    /// <summary>与 LabelPrintForZY 一致：先清绑定再调 JS 清空 DOM 并聚焦。</summary>
    async Task ClearBarcodeScanInputAsync()
    {
        Barcode = string.Empty;
        await InvokeAsync(StateHasChanged);
        await JS.InvokeVoidAsync("blazorClearScanInputs");
        await Task.Delay(1);
    }

    async Task SearchOrderAsync()
    {
        if (_isSearching) return;

        if (string.IsNullOrWhiteSpace(WorkNo))
        {
            SnackbarHelper.Show("请输入生产订单", Severity.Warning);
            return;
        }

        _isSearching = true;
        _orderData = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var langParam = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "1" : "E";
            var response = await SapEwmService.InvokeDynamicAsync(
                "GetProductInfo",
                new Dictionary<string, string?> { ["po"] = WorkNo.Trim(), ["lang"] = langParam },
                logToBrowserConsole: true);

            if (response.Code != ApiResultCode.Success || response.Data is null || !response.Data.Success)
            {
                SnackbarHelper.Show(response.Message ?? response.Data?.Error ?? "未找到产品信息", Severity.Error);
                return;
            }

            var orderNo = response.Data.GetString("orderNo");
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            _orderData = response.Data;
            WorkNo = ClearFrontZero(orderNo);
            MaterialNo = ClearFrontZero(response.Data.GetString("materialCode"));
            var qty = response.Data.GetString("orderQuantity");
            BindSum = string.IsNullOrWhiteSpace(qty)
                ? string.Empty
                : Math.Round(Convert.ToDecimal(qty, CultureInfo.InvariantCulture), 0).ToString(CultureInfo.InvariantCulture);
            Supplier = response.Data.GetString("LGORT");

            BoxNum = (await Ld40Binding.GetNextCaseNumberAsync(WorkNo)).ToString(CultureInfo.InvariantCulture);
            AppendLog($"查询成功：物料 {MaterialNo}，总数 {BindSum}，箱号 {BoxNum}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    async Task OnBarcodeKeyUp(KeyboardEventArgs e)
    {
        var isEnter = string.Equals(e.Key, "Enter", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(e.Code, "Enter", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(e.Code, "NumpadEnter", StringComparison.OrdinalIgnoreCase);
        if (!isEnter) return;
        await ProcessBarcodeScanAsync();
    }

    /// <summary>
    /// 外套管扫码（回车）：校验 NG/已绑定/重码后仅追加到条码列表，不回写 LD40 外箱号。
    /// </summary>
    async Task ProcessBarcodeScanAsync()
    {
        var code = (Barcode ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(code))
        {
            await ClearBarcodeScanInputAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkNo))
        {
            SnackbarHelper.Show("先输入生产订单！", Severity.Warning);
            await ClearBarcodeScanInputAsync();
            return;
        }

        if (IsBarcodeDuplicateInList(code))
        {
            SnackbarHelper.Show("条码重复，请检查！", Severity.Warning);
            await ClearBarcodeScanInputAsync();
            return;
        }

        if (code.Length != BarcodeLength)
        {
            SnackbarHelper.Show("条码长度异常，请检查！", Severity.Warning);
            await ClearBarcodeScanInputAsync();
            return;
        }

        int check;
        string? boundOuter;
        try
        {
            (check, boundOuter) = await Ld40Binding.CheckBarcodeAsync(code);
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"查询 LD40 失败：{ex.Message}", Severity.Error);
            AppendLog($"LD40 查询异常：{ex.Message}");
            await ClearBarcodeScanInputAsync();
            return;
        }

        if (check == 1)
        {
            SnackbarHelper.Show($"条码：{code}，检验不合格或无检测记录，请检查！", Severity.Warning);
            await ClearBarcodeScanInputAsync();
            return;
        }

        if (check == 2)
        {
            SnackbarHelper.Show($"条码：{code}，已绑定外箱：{boundOuter}，重复绑定！", Severity.Warning);
            await ClearBarcodeScanInputAsync();
            return;
        }

        BarcodeList += code + "/";
        BindNumber = (int.Parse(string.IsNullOrWhiteSpace(BindNumber) ? "0" : BindNumber, CultureInfo.InvariantCulture) + 1)
            .ToString(CultureInfo.InvariantCulture);
        AppendLog($"已录入：{code}，当前 {BindNumber}/{MaxBindPerBox}");

        var count = int.Parse(BindNumber, CultureInfo.InvariantCulture);
        if (count == MaxBindPerBox)
        {
            SnackbarHelper.Show("数量已满,开始打印外箱标签！", Severity.Info);
            _saveState = "满箱打印";
            await PrintAndSaveAsync();
        }
        else if (count > MaxBindPerBox)
        {
            SnackbarHelper.Show("数量已超24台,请检查！", Severity.Warning);
        }

        await ClearBarcodeScanInputAsync();
    }

    bool IsBarcodeDuplicateInList(string code)
    {
        if (string.IsNullOrWhiteSpace(BarcodeList)) return false;
        if (BarcodeList.Contains(code, StringComparison.Ordinal)) return true;
        return ParseBarcodeList().Any(b => string.Equals(b, code, StringComparison.OrdinalIgnoreCase));
    }

    List<string> ParseBarcodeList()
        => BarcodeList.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrEmpty(b))
            .ToList();

    async Task PrintSubmitAsync()
    {
        if (_isPrinting) return;
        _saveState = "人工打印";
        await PrintAndSaveAsync();
    }

    async Task PrintAndSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkNo))
        {
            SnackbarHelper.Show("请输入生产订单", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Worker))
        {
            SnackbarHelper.Show("请输入员工工号", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(BindSum))
        {
            SnackbarHelper.Show("请输入总数", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            SnackbarHelper.Show("请先选择打印机", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(BoxText))
        {
            SnackbarHelper.Show("侧唛文本不能为空", Severity.Warning);
            return;
        }

        var outerBox = GetCurrentOuterBoxBarcode();
        if (outerBox is null)
        {
            SnackbarHelper.Show("请先查询生产订单以获取箱号！", Severity.Warning);
            return;
        }

        _isPrinting = true;
        AppendLog($"开始打印任务… {_saveState}");
        await InvokeAsync(StateHasChanged);

        try
        {
            var scannedBarcodes = ParseBarcodeList();
            if (scannedBarcodes.Count > 0)
            {
                if (!await BindBarcodesToOuterBoxAsync(outerBox, scannedBarcodes))
                    return;
            }

            var layout = Ld40PrintLayoutBuilder.Build(SelectedPrinter!, outerBox, BoxText, Worker, DefaultFontSizePt);
            await PrinterHelper.PrintLayoutAsync(JS, layout);
            await PrinterHelper.SaveLastUsedPrinterAsync(JS, SelectedPrinter, PrinterStorageKey);
            AppendLog($"已发送打印：{SelectedPrinter}，外箱 {outerBox}");

            SnackbarHelper.Show("打印提交成功", Severity.Success);
            AppendLog("结束打印任务。");
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"异常：{ex.Message}", Severity.Error);
            AppendLog($"异常：{ex.Message}");
        }
        finally
        {
            _isPrinting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    async Task SearchBindingAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Seach_Barcode))
            {
                Search_value = await Ld40Binding.SearchLd40ByBarcodeAsync(Seach_Barcode.Trim()) ?? string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(Seach_BoxNo))
            {
                SnackbarHelper.Show("请输入查询箱条码或外套管条码", Severity.Warning);
                return;
            }

            Search_value = await Ld40Binding.GetAssemblyBarcodeByOuterBoxAsync(Seach_BoxNo.Trim()) ?? string.Empty;
            if (string.IsNullOrEmpty(Search_value))
                Search_value = "未查询到绑定记录。";
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"查询失败：{ex.Message}", Severity.Error);
        }
    }

    async Task ClearSearchAsync()
    {
        Search_value = string.Empty;
        Seach_BoxNo = string.Empty;
        Seach_Barcode = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 将条码列表绑定到当前外箱：回写 LD40.Outer_box_barcode，并写入 JClogin 绑定表（与 WinForms 一致）。
    /// </summary>
    async Task<bool> BindBarcodesToOuterBoxAsync(string outerBox, List<string> barcodes)
    {
        AppendLog($"开始绑定 {barcodes.Count} 条外套管条码 → 外箱 {outerBox}");

        if (!await Ld40Binding.UpdateLd40OuterBoxBindingBatchAsync(outerBox, BarcodeList))
        {
            SnackbarHelper.Show($"回写 LD40 外箱绑定失败，外箱：{outerBox}", Severity.Error);
            AppendLog($"LD40 外箱绑定失败：{outerBox}");
            return false;
        }

        AppendLog($"LD40 已写入外箱号：{outerBox}（{string.Join(", ", barcodes)}）");

        var assemblyList = TrimTrailingSlash(BarcodeList);
        var flag = await Ld40Binding.SaveAssemblyOuterCasingBindingAsync(
            outerBox,
            assemblyList,
            Worker.Trim(),
            MaterialNo.Trim(),
            _saveState,
            BoxNum.Trim(),
            BindNumber.Trim(),
            WorkNo.Trim());

        if (flag != "suc")
        {
            SnackbarHelper.Show($"保存绑定记录失败：{flag}", Severity.Error);
            AppendLog($"保存绑定记录失败：{flag}");
            return false;
        }

        BarcodeList = string.Empty;
        BindNumber = "0";
        BoxNum = (await Ld40Binding.GetNextCaseNumberAsync(WorkNo)).ToString(CultureInfo.InvariantCulture);
        AppendLog($"绑定记录已保存，下一箱号：{BoxNum}");
        SnackbarHelper.Show($"已绑定 {barcodes.Count} 条条码到外箱 {outerBox}", Severity.Success);
        await InvokeAsync(StateHasChanged);
        return true;
    }

    string? GetCurrentOuterBoxBarcode()
    {
        var orderNo = WorkNo.Trim();
        if (string.IsNullOrEmpty(orderNo)) return null;
        if (!int.TryParse(BoxNum.Trim(), out var caseNum)) return null;
        return $"{orderNo}-{caseNum:D3}";
    }

    static string TrimTrailingSlash(string list)
    {
        var s = list.Trim();
        while (s.EndsWith('/'))
            s = s[..^1];
        return s;
    }

    static string ClearFrontZero(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] != '0')
                return str[i..];
        }
        return "0";
    }

    void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:MM/dd HH:mm:ss}] -->> {message}";
        PrintMessages = string.IsNullOrEmpty(PrintMessages) ? line : PrintMessages + Environment.NewLine + line;
    }
}
