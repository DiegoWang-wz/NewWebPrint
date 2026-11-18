using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Newtonsoft.Json;
using NewWebPrint.DataDtos;
using NewWebPrint.Models.Api;
using NewWebPrint.Services.SapEwm;
using NewWebPrint.Utilities;

namespace NewWebPrint.Components.Pages.铭牌标签;

public partial class LabelPrintForNameplate
{
    const string HangRailTemplateFile = "智能家居挂衣杆铭牌.json";
    const string PrinterStorageKey = "Nameplate_printer";

    string ProductionOrder { get; set; } = "";
    string DeliveryOrSalesOrderNo { get; set; } = "";
    bool IsSearching { get; set; }
    bool IsPrinting { get; set; }
    string LogMessages { get; set; } = string.Empty;

    string? SelectedPrinter { get; set; }
    List<string> Printers { get; set; } = [];
    DateTime? ProductionDate { get; set; } = DateTime.Today;
    int SerialStart { get; set; } = 1;
    /// <summary>套数：每套打印 RIGHT + LEFT 各一张。</summary>
    int PrintPairs { get; set; } = 1;

    SapEwmDynamicPayload? _data;
    List<SapEwmDynamicPayload> _tableRows = [];

    string PreviewMaterialLine =>
        string.IsNullOrWhiteSpace(_data?.GetString("materialName"))
            ? "(物料名称)"
            : (_data!.GetString("materialName") ?? "");

    string PreviewSerialLine
    {
        get
        {
            var code = (_data?.GetString("materialCode") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                return "(物料号-日期-流水号)";
            return BuildSerialLine(code, ProductionDate ?? DateTime.Today, SerialStart);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await PrinterBootstrap.TryLoadOnFirstRenderAsync(
            firstRender,
            PrinterStorageKey,
            r =>
            {
                Printers = r.Printers?.ToList() ?? [];
                SelectedPrinter = r.SelectedPrinter;
            },
            () => InvokeAsync(StateHasChanged));
    }

    async Task OnSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            await SearchSubmitAsync();
    }

    async Task SearchSubmitAsync()
    {
        if (IsSearching)
            return;

        var po = ProductionOrder?.Trim();
        var so = DeliveryOrSalesOrderNo?.Trim();
        if (string.IsNullOrWhiteSpace(po) && string.IsNullOrWhiteSpace(so))
        {
            SnackbarHelper.Show("请输入生产订单号或销售/发货订单号", Severity.Warning);
            return;
        }

        IsSearching = true;
        _data = null;
        _tableRows = [];
        await InvokeAsync(StateHasChanged);

        try
        {
            List<SapEwmDynamicPayload> resultRows;
            if (!string.IsNullOrWhiteSpace(po))
            {
                AppendLog($"按生产订单查询：{po}");
                var response = await SapEwmService.InvokeDynamicAsync(
                    "GetProductInfo",
                    new Dictionary<string, string?> { ["po"] = po, ["lang"] = "1" },
                    logToBrowserConsole: true);
                if (response.Code != ApiResultCode.Success || response.Data is null)
                {
                    SnackbarHelper.Show(response.Message, Severity.Error);
                    AppendLog($"查询失败：{response.Message}");
                    return;
                }

                resultRows = [NormalizeSapPayload(response.Data)];
            }
            else
            {
                AppendLog($"按销售/发货订单查询：{so}");
                resultRows = await QueryBySalesOrDeliveryAsync(so!);
                if (resultRows.Count == 0)
                {
                    SnackbarHelper.Show("未找到对应订单数据", Severity.Warning);
                    AppendLog("未找到对应订单数据");
                    return;
                }
            }

            _tableRows = resultRows;
            _data = _tableRows[0];
            AppendLog($"查询成功，共 {_tableRows.Count} 行；当前物料 {_data.GetString("materialCode")}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"查询异常：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// 智能家居挂衣杆铭牌：每套打印 RIGHT BODY + LEFT BODY 各一张。
    /// title=TYPE:xxx BODY；material_des=物料名称；serialnum=物料号-yyMMdd-流水。
    /// </summary>
    async Task PrintHangRailNameplateAsync()
    {
        if (IsPrinting)
            return;

        if (_data is null)
        {
            SnackbarHelper.Show("请先查询并选择订单行", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            SnackbarHelper.Show("请先选择打印机", Severity.Warning);
            return;
        }

        var materialName = (_data.GetString("materialName") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(materialName))
        {
            SnackbarHelper.Show("物料名称为空，无法打印", Severity.Warning);
            return;
        }

        var materialCode = (_data.GetString("materialCode") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(materialCode))
        {
            SnackbarHelper.Show("物料号为空，无法打印", Severity.Warning);
            return;
        }

        if (PrintPairs < 1)
        {
            SnackbarHelper.Show("套数至少为 1", Severity.Warning);
            return;
        }

        if (SerialStart < 1)
        {
            SnackbarHelper.Show("流水号起始至少为 1", Severity.Warning);
            return;
        }

        const int maxPairs = 500;
        if (PrintPairs > maxPairs)
        {
            SnackbarHelper.Show($"套数过大（一次最多 {maxPairs}）", Severity.Warning);
            return;
        }

        var templatePath = Path.Combine(Env.WebRootPath, "templates", HangRailTemplateFile);
        if (!File.Exists(templatePath))
        {
            SnackbarHelper.Show($"未找到模板：wwwroot/templates/{HangRailTemplateFile}", Severity.Error);
            return;
        }

        string templateJson;
        try
        {
            templateJson = await File.ReadAllTextAsync(templatePath);
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"读取模板失败：{ex.Message}", Severity.Error);
            return;
        }

        var date = ProductionDate ?? DateTime.Today;
        var dateText = date.ToString("yyMMdd", CultureInfo.InvariantCulture);

        IsPrinting = true;
        AppendLog($"挂衣杆铭牌：物料 {materialCode} / {materialName}，日期 {dateText}，流水 {SerialStart}～{SerialStart + PrintPairs - 1}，{PrintPairs} 套（每套 RIGHT+LEFT）");
        await InvokeAsync(StateHasChanged);

        try
        {
            for (var i = 0; i < PrintPairs; i++)
            {
                var serial = SerialStart + i;
                var serialLine = BuildSerialLine(materialCode, date, serial);

                foreach (var title in new[] { "TYPE:RIGHT BODY", "TYPE:LEFT BODY" })
                {
                    var layout = JsonConvert.DeserializeObject<PrintLayoutModel>(templateJson);
                    if (layout == null)
                    {
                        SnackbarHelper.Show("模板解析为空", Severity.Error);
                        return;
                    }

                    layout.PrinterName = SelectedPrinter;
                    layout.PaperWidthMm = 55f;
                    layout.PaperHeightMm = 40f;
                    layout.Copies = 1;
                    layout.StartValue = 1;
                    layout.PrintCount = 1;
                    layout.BorderThicknessMm = 0.2f;
                    layout.IsRotate = false;

                    // title：LEFT/RIGHT；material_des：物料名称（TYPE 内容）；serialnum：物料号-日期-流水
                    SetBlockText(layout, "title", title);
                    SetBlockText(layout, "material_des", materialName);
                    SetBlockText(layout, "serialnum", serialLine);

                    await PrinterHelper.PrintLayoutAsync(JS, layout);
                    AppendLog($"已提交：{title} / {serialLine}");
                }
            }

            SerialStart += PrintPairs;
            await PrinterHelper.SaveLastUsedPrinterAsync(JS, SelectedPrinter, PrinterStorageKey);
            AppendLog($"打印提交成功，下一起始流水号 {SerialStart}");
            SnackbarHelper.Show("打印提交成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"打印异常：{ex.Message}");
            SnackbarHelper.Show($"异常：{ex.Message}", Severity.Error);
        }
        finally
        {
            IsPrinting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    static string BuildSerialLine(string materialCode, DateTime date, int serial) =>
        $"{materialCode}-{date.ToString("yyMMdd", CultureInfo.InvariantCulture)}-{serial.ToString("D5", CultureInfo.InvariantCulture)}";

    static void SetBlockText(PrintLayoutModel layout, string blockName, string content)
    {
        var block = layout.Blocks?.FirstOrDefault(b =>
            string.Equals(b.Name, blockName, StringComparison.OrdinalIgnoreCase));
        if (block?.Text == null)
            return;
        block.Text.Content = content ?? string.Empty;
    }

    async Task<List<SapEwmDynamicPayload>> QueryBySalesOrDeliveryAsync(string so)
    {
        var saleResponse = await SapEwmService.InvokeAsync(
            "GetSaleInfoCM",
            new Dictionary<string, string?> { ["so"] = so },
            logToBrowserConsole: true);
        var rows = ExtractPayloadListFromInvoke(saleResponse.Data)
            .Select(NormalizeSapPayload)
            .ToList();
        if (rows.Count > 0)
            return rows;

        var deliveryResponse = await SapEwmService.InvokeAsync(
            "GetDeliveryInfoCM",
            new Dictionary<string, string?> { ["so"] = so },
            logToBrowserConsole: true);
        return ExtractPayloadListFromInvoke(deliveryResponse.Data)
            .Select(NormalizeSapPayload)
            .ToList();
    }

    List<SapEwmDynamicPayload> ExtractPayloadListFromInvoke(SapEwmInvokeResult? invokeResult)
    {
        var result = new List<SapEwmDynamicPayload>();
        if (invokeResult is null || !invokeResult.Success)
            return result;

        var raw = invokeResult.ResultText ?? invokeResult.JsonResult;
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                result.Add(SapEwmDynamicPayload.FromJson(
                    doc.RootElement.GetRawText(),
                    invokeResult.RawSoapResponse,
                    invokeResult.Endpoint,
                    invokeResult.Network,
                    invokeResult.SoapVersion));
                return result;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    result.Add(SapEwmDynamicPayload.FromJson(
                        item.GetRawText(),
                        invokeResult.RawSoapResponse,
                        invokeResult.Endpoint,
                        invokeResult.Network,
                        invokeResult.SoapVersion));
                }
            }
        }
        catch
        {
            // ignore parse errors; caller shows empty
        }

        return result;
    }

    void OnTableRowClick(TableRowClickEventArgs<SapEwmDynamicPayload> args)
    {
        if (args.Item is null)
            return;

        _data = args.Item;
        AppendLog($"已选择：{_data.GetString("orderNo")} / {_data.GetString("materialCode")}");
    }

    string GetSelectedRowClass(SapEwmDynamicPayload row, int _) =>
        _data is not null && ReferenceEquals(row, _data) ? "nameplate-selected-row" : string.Empty;

    SapEwmDynamicPayload NormalizeSapPayload(SapEwmDynamicPayload payload)
    {
        var map = new Dictionary<string, JsonElement>(payload.Fields, StringComparer.OrdinalIgnoreCase);

        SetJsonField(map, "orderNo", TrimLeadingZeros(payload.GetString("orderNo")));
        SetJsonField(map, "SalesOrderNo", TrimLeadingZeros(payload.GetString("SalesOrderNo")));
        SetJsonField(map, "materialCode", TrimLeadingZeros(payload.GetString("materialCode")));

        var customerName = FirstNonEmpty(payload, "customerName", "client");
        var customerCode = TrimLeadingZeros(FirstNonEmpty(payload, "customerCode", "clientno"));
        var materialCodeByClient = FirstNonEmpty(payload, "materialCodebyClient", "goodsmark");
        var materialNameByClient = FirstNonEmpty(payload, "materialNamebyClient", "materialModel_en");
        var zogen = FirstNonEmpty(payload, "ZOGEN", "ZCPON");
        var jclogo = FirstNonEmpty(payload, "jclogoText", "HaveJiecangLOGO");
        var zi10 = FirstNonEmpty(payload, "ZI10", "STTXT");
        var zmType = FirstNonEmpty(payload, "ZMTYPE", "中文标识");

        SetJsonField(map, "customerName", customerName);
        SetJsonField(map, "client", customerName);
        SetJsonField(map, "customerCode", customerCode);
        SetJsonField(map, "clientno", customerCode);
        SetJsonField(map, "materialCodebyClient", materialCodeByClient);
        SetJsonField(map, "goodsmark", materialCodeByClient);
        SetJsonField(map, "materialNamebyClient", materialNameByClient);
        SetJsonField(map, "materialModel_en", materialNameByClient);
        SetJsonField(map, "ZOGEN", zogen);
        SetJsonField(map, "ZCPON", zogen);
        SetJsonField(map, "jclogoText", jclogo);
        SetJsonField(map, "HaveJiecangLOGO", jclogo);
        SetJsonField(map, "ZI10", zi10);
        SetJsonField(map, "STTXT", zi10);
        SetJsonField(map, "ZMTYPE", zmType);
        SetJsonField(map, "中文标识", zmType);

        return new SapEwmDynamicPayload(
            payload.Success,
            payload.RawJson,
            map,
            payload.RawSoapResponse,
            payload.Error,
            payload.InvokeEndpoint,
            payload.InvokeNetwork,
            payload.InvokeSoapVersion);
    }

    static string? FirstNonEmpty(SapEwmDynamicPayload payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = payload.GetString(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    static void SetJsonField(Dictionary<string, JsonElement> map, string key, string? value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
        using var doc = JsonDocument.Parse(json);
        map[key] = doc.RootElement.Clone();
    }

    static string TrimLeadingZeros(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        var i = 0;
        while (i < text.Length - 1 && text[i] == '0')
            i++;
        return text[i..];
    }

    void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        LogMessages = string.IsNullOrEmpty(LogMessages) ? line : LogMessages + Environment.NewLine + line;
    }
}
