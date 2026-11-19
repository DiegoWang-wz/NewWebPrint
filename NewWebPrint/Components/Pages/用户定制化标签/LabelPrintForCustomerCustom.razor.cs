using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Newtonsoft.Json;
using NewWebPrint.DataDtos;
using NewWebPrint.Models;
using NewWebPrint.Models.Api;
using NewWebPrint.Services.SapEwm;
using NewWebPrint.Utilities;

namespace NewWebPrint.Components.Pages.用户定制化标签;

public partial class LabelPrintForCustomerCustom
{
    const string TemplateFileName = "JCH35S1.json";
    const string PrinterStorageKey = "CustomerCustom_printer";
    const string BlockNameType = "物料名称";
    const string BlockNameMaterial = "物料号";
    const string BlockNameSerial = "流水号";

    [Inject] private ISapEwmService SapEwmService { get; set; } = null!;

    string MaterialNo { get; set; } = string.Empty;
    bool IsSearching { get; set; }
    bool IsPrinting { get; set; }
    List<EWMLabelModel> ResultList { get; set; } = [];
    EWMLabelModel? SelectedModel { get; set; }
    string LogMessages { get; set; } = string.Empty;

    string? SelectedPrinter { get; set; }
    List<string> Printers { get; set; } = [];
    DateTime? ProductionDate { get; set; } = DateTime.Today;
    int SerialStart { get; set; } = 1;
    int PrintCount { get; set; } = 1;

    string PreviewTypeLine =>
        SelectedModel == null ? "" : BuildTypeLine(SelectedModel);

    string PreviewOcLine =>
        SelectedModel == null ? "" : BuildOcLine(SelectedModel);

    string PreviewSerialLine =>
        BuildSerialLine(ProductionDate ?? DateTime.Today, SerialStart);

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

    async Task OnMaterialKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            await SearchByMaterialAsync();
    }

    async Task SearchByMaterialAsync()
    {
        if (IsSearching)
            return;

        var material = (MaterialNo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(material))
        {
            SnackbarHelper.Show("请输入物料号", Severity.Warning);
            return;
        }

        IsSearching = true;
        ResultList = [];
        SelectedModel = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            AppendLog($"开始按物料号查询：{material}");
            var list = await GetEwmLabelModelByMaterialAsync(material);
            if (list == null || list.Count == 0)
            {
                AppendLog($"未找到物料信息：{material}");
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            ResultList = list;
            SelectedModel = list[0];
            AppendLog($"查询成功：{SelectedModel.materialCode} / {SelectedModel.materialName}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"查询失败：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// 对齐物料标签页：仅物料号调用 GetMaterialModel_lang（工厂传空）。
    /// </summary>
    async Task<List<EWMLabelModel>?> GetEwmLabelModelByMaterialAsync(string material)
    {
        var jsondtr = await GetMaterialModelLangAsync("", material, "1");
        if (string.IsNullOrWhiteSpace(jsondtr))
            return null;

        var materialModel = PrinterHelper.JsonToObject<SAPMaterialModel>(jsondtr);
        if (materialModel == null || string.IsNullOrWhiteSpace(materialModel.newCode))
            return null;

        return
        [
            new EWMLabelModel
            {
                materialGroup = materialModel.MGroup ?? "",
                materialName = materialModel.Mname ?? "",
                materialCode = materialModel.newCode ?? "",
                materialModel_zh = materialModel.Mmodel ?? "",
                orderNo = "0000000000",
                orderLineNo = "00",
                orderQuantity = "999999",
                SalesOrderNo = "",
                SalesOrderNolineNO = "",
                reportNum = "0"
            }
        ];
    }

    async Task<string> GetMaterialModelLangAsync(string oldCode, string newCode, string lang = "1")
    {
        var resp = await SapEwmService.InvokeAsync(
            "GetMaterialModel_lang",
            new Dictionary<string, string?>
            {
                ["oldCode"] = oldCode ?? "",
                ["newCode"] = newCode ?? "",
                ["lang"] = lang
            },
            logToBrowserConsole: true);

        if (resp.Code != ApiResultCode.Success || resp.Data is null)
        {
            AppendLog($"接口失败：{resp.Message}");
            return string.Empty;
        }

        return resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty;
    }

    async Task PrintSubmitAsync()
    {
        if (IsPrinting)
            return;

        if (SelectedModel == null)
        {
            SnackbarHelper.Show("请先查询并选择物料", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            SnackbarHelper.Show("请先选择打印机", Severity.Warning);
            return;
        }

        if (PrintCount < 1)
        {
            SnackbarHelper.Show("打印数量至少为 1", Severity.Warning);
            return;
        }

        if (SerialStart < 1)
        {
            SnackbarHelper.Show("流水号起始至少为 1", Severity.Warning);
            return;
        }

        var templatePath = Path.Combine(Env.WebRootPath, "templates", TemplateFileName);
        if (!File.Exists(templatePath))
        {
            SnackbarHelper.Show($"未找到模板：wwwroot/templates/{TemplateFileName}", Severity.Error);
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
        var typeLine = BuildTypeLine(SelectedModel);
        var ocLine = BuildOcLine(SelectedModel);

        IsPrinting = true;
        AppendLog($"开始打印：{SelectedModel.materialCode}，日期 {date:yyyyMMdd}，流水 {SerialStart}～{SerialStart + PrintCount - 1}，共 {PrintCount} 张");
        await InvokeAsync(StateHasChanged);

        try
        {
            for (var i = 0; i < PrintCount; i++)
            {
                var serial = SerialStart + i;
                var layout = JsonConvert.DeserializeObject<PrintLayoutModel>(templateJson);
                if (layout == null)
                {
                    SnackbarHelper.Show("模板解析为空", Severity.Error);
                    return;
                }

                layout.PrinterName = SelectedPrinter;
                layout.PaperWidthMm = 25f;
                layout.PaperHeightMm = 15f;
                layout.Copies = 1;
                layout.StartValue = 1;
                layout.PrintCount = 1;
                layout.BorderThicknessMm = 0.2f;
                layout.IsRotate = false;

                SetBlockText(layout, BlockNameType, typeLine);
                SetBlockText(layout, BlockNameMaterial, ocLine);
                SetBlockText(layout, BlockNameSerial, BuildSerialLine(date, serial));

                await PrinterHelper.PrintLayoutAsync(JS, layout);
                AppendLog($"已提交：{BuildSerialLine(date, serial)}");
            }

            SerialStart += PrintCount;
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

    static string BuildTypeLine(EWMLabelModel model) =>
        (model.materialName ?? string.Empty).Trim();

    static string BuildOcLine(EWMLabelModel model) =>
        (model.materialCode ?? string.Empty).Trim();

    static string BuildSerialLine(DateTime date, int serial) =>
        date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        + "-" + serial.ToString("D5", CultureInfo.InvariantCulture);

    static void SetBlockText(PrintLayoutModel layout, string blockName, string content)
    {
        var block = layout.Blocks?.FirstOrDefault(b =>
            string.Equals(b.Name, blockName, StringComparison.OrdinalIgnoreCase));
        if (block?.Text == null)
            return;
        block.Text.Content = content ?? string.Empty;
    }

    void OnRowClicked(TableRowClickEventArgs<EWMLabelModel> args)
    {
        if (args.Item == null)
            return;

        SelectedModel = args.Item;
        AppendLog($"已选择物料：{SelectedModel.materialCode}");
    }

    string SelectedRowClass(EWMLabelModel item, int index) =>
        ReferenceEquals(item, SelectedModel) ? "selected-row" : string.Empty;

    void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        LogMessages = string.IsNullOrEmpty(LogMessages) ? line : LogMessages + Environment.NewLine + line;
    }
}
