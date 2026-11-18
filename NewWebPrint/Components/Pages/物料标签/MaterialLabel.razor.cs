using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using NewWebPrint.DataDtos;
using NewWebPrint.Models;
using NewWebPrint.Models.Api;
using NewWebPrint.Services.SapEwm;
using NewWebPrint.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sap.Data.Hana;

namespace NewWebPrint.Components.Pages.物料标签;

public partial class MaterialLabel
{
    [Inject] private ISapEwmService SapEwmService { get; set; } = null!;
    [Inject] private IBrowserViewportService BrowserViewportService { get; set; } = null!;
    [Inject] private LocalAgentPrinterBootstrap PrinterBootstrap { get; set; } = null!;

    /// <summary>对齐 WinForm 构造参数 type；URL ?type=smt 启用 V2 新码。</summary>
    [SupplyParameterFromQuery(Name = "type")]
    public string? LaunchType { get; set; }

    #region 公共 — 字段与生命周期

    private List<EWMLabelModel> printModelList = new();
    private EWMLabelModel? printModel;
    /// <summary>常规标签无有效 HU 时的占位（与 WinForm 一致，12 个 0）。</summary>
    const string HuPlaceholder = "000000000000";
    string _currentLabelUniqueCodeV2 = string.Empty;
    private bool IsSearching;
    private string? SelectedPrinter;
    private string? DefaultPrinter;
    private List<string> Printers = new();
    private string? LSP;
    private string? batchNo;
    private string? QrValue;
    private string location_code = string.Empty;
    private string _customerPoNoForLabel = string.Empty;
    private bool IsPrinting;
    private bool IsGenerating;
    private MudTabs? tabsRef;
    private int activeTabIndex;
    /// <summary>由 purchaseFunction 控制可见 Tab；默认全开。</summary>
    List<int> VisibleLogicalTabs { get; set; } = [0, 1, 2, 3, 4];
    /// <summary>当前面板对应的业务 Tab 索引（对齐 WinForm SelectedTab）。</summary>
    int LogicalTab =>
        activeTabIndex >= 0 && activeTabIndex < VisibleLogicalTabs.Count
            ? VisibleLogicalTabs[activeTabIndex]
            : 0;
    bool ShowLogicalTab(int logical) => VisibleLogicalTabs.Contains(logical);

    public string ProdOrderNo { get; set; } = string.Empty;
    public string ProdRemark { get; set; } = string.Empty;
    public string PurchOrderNo { get; set; } = string.Empty;
    public string PurchLineNo { get; set; } = "0";
    public string PrintFactory { get; set; } = "1100";
    public string PrintSalesOrderNo { get; set; }
    public string PrintSalesLineNo { get; set; }
    public string PrintRemark { get; set; }
    public string PrintMaterialNo { get; set; }
    public string PrintPurchOrderNo { get; set; }
    public string PrintPurchLineNo { get; set; }
    public bool PrintIsStock { get; set; } = false;
    /// <summary>由下方「马来专属」驱动，对应原 Only MAS 版式。</summary>
    bool PrintIsMalaysia => string.Equals(SpecialPrint, "马来专属", StringComparison.Ordinal);
    /// <summary>由下方「电子料纸箱」驱动，对应原 Only MAS2 版式。</summary>
    bool PrintIsMalaysia2 => string.Equals(SpecialPrint, "电子料纸箱", StringComparison.Ordinal);
    public string SalesOrderNo { get; set; } = string.Empty;
    public string StockDeliveryOrderNo { get; set; } = string.Empty;
    public string PO_NO { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string WeightStandard { get; set; } = string.Empty;
    public string WeightMeasured { get; set; } = string.Empty;
    public string WeightTolerance { get; set; } = string.Empty;
    private bool _isSerialOpen;
    private DotNetObjectReference<MaterialLabel>? _objRef;
    const string ValidWeightUserId = "100660";
    const string WeightStandardStorageKey = "MaterialLabel_weightStandard";
    const string WeightToleranceStorageKey = "MaterialLabel_weightTolerance";
    bool CanUseWeightControls =>
        string.Equals(UserId?.Trim(), ValidWeightUserId, StringComparison.Ordinal);
    public string BoxNum { get; set; } = string.Empty;
    public string SumNum { get; set; } = string.Empty;
    public string PrintCount { get; set; } = "1";

    public string PaperSize { get; set; } = "60*40";
    public string PrintMessages { get; set; } = string.Empty;
    private string? QrImage;
    private string SpecialPrint { get; set; } = "无";
    public bool if_rotate { get; set; } = false;
    public bool if_qualify { get; set; } = true;
    public bool if_remark { get; set; } = true;
    /// <summary>对齐 WinForm checkBox_nobatch：仅 100×80 + 生产入库时隐藏批次。</summary>
    public bool HideBatch { get; set; }

    private Margin DynamicMargin = Margin.Dense;
    private int DynamicSpacing = 2;
    private Guid _subscriptionId = Guid.NewGuid();
    string _currentTray = string.Empty;
    string _totalTray = string.Empty;

    const string PrintSettingsStorageKey = "MaterialLabel_printSettings";
    const string LegacyPrinterStorageKey = "MaterialLabel_printer";

    sealed class MaterialLabelPrintSettings
    {
        public string? SelectedPrinter { get; set; }
        public string PaperSize { get; set; } = "60*40";
        public string SpecialPrint { get; set; } = "无";
        public bool if_rotate { get; set; }
        public bool if_qualify { get; set; } = true;
        public bool if_remark { get; set; } = true;
        public bool HideBatch { get; set; }
    }


    protected override async Task OnInitializedAsync()
    {
        _objRef = DotNetObjectReference.Create(this);
        await LoadPrintSettingsAsync();
        await LoadWeightSettingsAsync();
        ApplyPurchaseFunctionConfig();
        ApplyAutoRemarkOrderConfig();
        ApplyMalaysiaConfigDefault();
    }

    void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        PrintMessages = (PrintMessages ?? string.Empty) + line + Environment.NewLine;
    }

    string GetHanaConnectionString() =>
        Config["HanaConnStr"] ?? "Server=192.168.81.114:30065;UserID=SAPHANADB;Password=Init1234";

    string GetHanaConnectionStringSap600() =>
        Config["HanaConnStr600"] ?? "Server=192.168.83.4:31015;UserID=SAPHANADB;Password=Jchana_2024";

    void ResetSearchState()
    {
        printModel = null;
        printModelList.Clear();
        PO_NO = string.Empty;
        _customerPoNoForLabel = string.Empty;
        QrImage = null;
    }

    string GetLangParam()
    {
        var configured = Config["applicationLanguage"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "1" : "E";
    }

    bool IsChineseLang() => GetLangParam() == "1";

    /// <summary>100×80 / 100×120 日期格：仅打印时刻，不含 Time（对齐 WinForm grid8）。</summary>
    string BuildLabelDateTimeText()
    {
        if (GetLangParam().Contains("E", StringComparison.OrdinalIgnoreCase))
            return DateTime.Now.ToString("dd/MM/yyyy") + "  " + DateTime.Now.ToString("HH:mm");
        return DateTime.Now.ToShortDateString() + "  " + DateTime.Now.ToString("HH:mm");
    }

    /// <summary>60×40 / 80×60 底行：打印时刻 + Time 天数差（对齐 WinForm grid16）。</summary>
    string BuildSmallLabelDateTimeWithTime() =>
        DateTime.Now.ToString("yyyy/MM/dd HH:mm") + "    Time:" + PrinterHelper.GetDaysDifference(printModel!.DGLTP);

    #endregion

    #region Tab0 生产入库 — 查询

    async Task<string> GetProductInfoAsync(string po, string lang = "1")
    {
        var response = await SapEwmService.InvokeDynamicAsync(
            "GetProductInfo",
            new Dictionary<string, string?> { ["po"] = po, ["lang"] = lang },
            logToBrowserConsole: true);

        if (response.Code != ApiResultCode.Success || response.Data is null)
            throw new InvalidOperationException(response.Message);

        var raw = response.Data.RawJson;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("未返回业务 JSON");

        return raw;
    }

    /// <summary>对齐 WinForm <c>GetEWMLabelModelSC</c>：GetProductInfo + 去前导零 + 备库标记。</summary>
    async Task<EWMLabelModel> GetEWMLabelModelSCAsync(string no, string lang)
    {
        location_code = string.Empty;
        var model = new EWMLabelModel();

        try
        {
            var json = await GetProductInfoAsync(no, lang);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var obj = JObject.Parse(json);
                location_code = obj["SALES_WERKS"]?.ToString() ?? string.Empty;
                model = JsonConvert.DeserializeObject<EWMLabelModel>(json) ?? model;
            }
        }
        catch
        {
            // 与 WinForm 一致：异常时返回空模型，由调用方根据 orderNo 判断
        }

        if (!string.IsNullOrEmpty(model.orderNo))
        {
            model.orderNo = PrinterHelper.ClearFrontZreo(model.orderNo);
            model.orderLineNo = PrinterHelper.ClearFrontZreo(model.orderLineNo);
            model.materialCode = PrinterHelper.ClearFrontZreo(model.materialCode);
            model.supplier = PrinterHelper.ClearFrontZreo(model.supplier);
            if (model.AUART == "01")
                model.supplier += "  备库";
        }

        return model;
    }

    /// <summary>对齐 WinForm <c>GetBstkdByAufnr</c>：从 V_AFPO_BSTKD 按生产订单取客户 PO（BSTKD）。</summary>
    Task<string> GetBstkdByAufnrAsync(string productionOrderNo) =>
        Task.Run(() =>
        {
            var aufnrKey = PrinterHelper.ClearFrontZreo(productionOrderNo?.Trim());
            if (string.IsNullOrEmpty(aufnrKey))
                return string.Empty;

            const string sql = """
                               SELECT BSTKD
                               FROM "SAPHANADB"."V_AFPO_BSTKD"
                               WHERE AUFNR_TRIM = :aufnr
                               """;

            try
            {
                using var conn = new HanaConnection(GetHanaConnectionString());
                conn.Open();
                using var cmd = new HanaCommand(sql, conn);
                cmd.Parameters.AddWithValue(":aufnr", aufnrKey);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return string.Empty;
                return reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString()?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BSTKD(V_AFPO_BSTKD) 查询失败: {ex.Message}");
                return string.Empty;
            }
        });

    async Task TryLoadCustomerPoForProductionOrderAsync(string productionOrderNo)
    {
        _customerPoNoForLabel = string.Empty;
        var bstkd = await GetBstkdByAufnrAsync(productionOrderNo);
        if (string.IsNullOrWhiteSpace(bstkd))
            return;

        _customerPoNoForLabel = bstkd;
        AppendLog($"PO: {bstkd}");
    }

    static string EscapeHanaStringLiteral(string? value) =>
        (value ?? string.Empty).Replace("'", "''");

    static string GetDataRowColumnValue(DataRow row, DataTable table, params string[] columnNames)
    {
        if (row == null || table == null)
            return string.Empty;

        foreach (var columnName in columnNames)
        {
            if (table.Columns.Contains(columnName))
                return row[columnName]?.ToString()?.Trim() ?? string.Empty;

            foreach (DataColumn column in table.Columns)
            {
                if (string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    return row[column]?.ToString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    static string GetMaterialCodeFromHanaRow(DataRow row, DataTable table)
    {
        var code = GetDataRowColumnValue(row, table,
            "FINAL_MATERIAL_NO", "MATNR", "MATERIAL_NO", "MATERIAL_CODE", "MATERIAL");
        if (!string.IsNullOrEmpty(code))
            return code;

        return table.Columns.Count > 0 ? row[0]?.ToString()?.Trim() ?? string.Empty : string.Empty;
    }

    static string GetMaterialDescFromHanaRow(DataRow row, DataTable table)
    {
        var desc = GetDataRowColumnValue(row, table,
            "MATERIAL_DESC", "MAKTX", "MAKTG", "MAT_DESC", "DESCRIPTION", "DESC");
        if (!string.IsNullOrEmpty(desc))
            return desc;

        if (table.Columns.Count > 1)
            return row[1]?.ToString()?.Trim() ?? string.Empty;

        return table.Columns.Count > 0 ? row[0]?.ToString()?.Trim() ?? string.Empty : string.Empty;
    }

    /// <summary>对齐 WinForm <c>QueryFinalMaterialNos</c>：TF_GET_FINAL_MATERIAL_NO（发国外物料编号）。</summary>
    Task<List<string>> QueryFinalMaterialNosAsync(string productionOrderNo) =>
        Task.Run(() =>
        {
            var result = new List<string>();
            var orderNo = EscapeHanaStringLiteral(productionOrderNo?.Trim());
            if (string.IsNullOrEmpty(orderNo))
                return result;

            var sql = $"SELECT * FROM TF_GET_FINAL_MATERIAL_NO('{orderNo}')";
            using var conn = new HanaConnection(GetHanaConnectionString());
            conn.Open();
            using var cmd = new HanaCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            var dt = PrinterHelper.ConvertDataReaderToDataTable(reader);
            foreach (DataRow row in dt.Rows)
            {
                var code = PrinterHelper.ClearFrontZreo(GetMaterialCodeFromHanaRow(row, dt));
                if (!string.IsNullOrEmpty(code) && !result.Contains(code))
                    result.Add(code);
            }

            return result;
        });

    /// <summary>对齐 WinForm <c>QueryMaterialDescriptions</c>：TF_GET_MATERIAL_DESC（SAP600）。</summary>
    Task<List<string>> QueryMaterialDescriptionsAsync(string materialCode) =>
        Task.Run(() =>
        {
            var result = new List<string>();
            var matnr = EscapeHanaStringLiteral(materialCode?.Trim());
            if (string.IsNullOrEmpty(matnr))
                return result;

            var sql = $"SELECT * FROM TF_GET_MATERIAL_DESC('{matnr}')";
            using var conn = new HanaConnection(GetHanaConnectionStringSap600());
            conn.Open();
            using var cmd = new HanaCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            var dt = PrinterHelper.ConvertDataReaderToDataTable(reader);
            foreach (DataRow row in dt.Rows)
            {
                var desc = GetMaterialDescFromHanaRow(row, dt);
                if (!string.IsNullOrEmpty(desc))
                    result.Add(desc);
            }

            return result;
        });

    sealed class TempLabelItem
    {
        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialDesc { get; set; } = string.Empty;
    }

    async Task<List<TempLabelItem>> LoadTempLabelItemsAsync(string productionOrderNo)
    {
        var items = new List<TempLabelItem>();
        var materialCodes = await QueryFinalMaterialNosAsync(productionOrderNo);
        foreach (var materialCode in materialCodes)
        {
            var descriptions = await QueryMaterialDescriptionsAsync(materialCode);
            foreach (var description in descriptions)
            {
                items.Add(new TempLabelItem
                {
                    MaterialCode = materialCode,
                    MaterialDesc = description
                });
            }
        }

        return items;
    }

    static string BuildTempLabelQrValue(string materialCode, string batchNo) =>
        "//" + materialCode + "//////BATCH/" + batchNo;

    /// <summary>对齐 WinForm：生产入库且非马来版式时打海外配套；V2(smt) 跳过。</summary>
    bool ShouldPrintTempBoxLabels()
    {
        if (UseProductInboundQrV2())
            return false;

        return LogicalTab == 0 && !PrintIsMalaysia && !PrintIsMalaysia2;
    }

    /// <summary>对齐 WinForm ShouldHideBatchForPrint：100×80 + 勾选无批次 + 生产入库。</summary>
    bool ShouldHideBatchForPrint() =>
        string.Equals(PaperSize, "100*80", StringComparison.Ordinal)
        && HideBatch
        && LogicalTab == 0;

    string GetBatchForDisplay() =>
        ShouldHideBatchForPrint() ? string.Empty : (batchNo ?? string.Empty);

    #endregion

    #region 公共 — SAP 批次

    async Task<string> GetBatchAsync()
    {
        var resp = await SapEwmService.GetBatchAsync();
        if (resp.Code != ApiResultCode.Success || resp.Data is null)
            return string.Empty;
        return resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty;
    }

    #endregion

    #region Tab1 采购收货 — 查询

    async Task<string> GetPurchaseInfoAsync(string po, string lineNo, string supplier, string lang = "1")
    {
        var resp = await SapEwmService.InvokeAsync(
            "GetpurchaseInfo",
            new Dictionary<string, string?>
            {
                ["Po"] = po,
                ["lineNo"] = lineNo,
                ["supplierCode"] = supplier,
                ["lang"] = lang
            },
            logToBrowserConsole: true);

        if (resp.Code != ApiResultCode.Success || resp.Data is null)
            throw new InvalidOperationException(resp.Message);

        var body = resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("未返回采购信息 JSON");

        return body;
    }

    /// <summary>对齐 WinForm <c>GetEWMLabelModelCG</c>。</summary>
    async Task<List<EWMLabelModel>?> GetEWMLabelModelCGAsync(string no, string lineno, string supplier, string lang)
    {
        var saleOrder = string.Empty;

        try
        {
            using var conn = new HanaConnection(GetHanaConnectionString());
            conn.Open();

            const string sql = "SELECT ZDJBH FROM ZTSD001_LOG " +
                               "WHERE ZTYPE = 'S' AND ZFLAG = 'C' AND ZYDJ = :no " +
                               "ORDER BY ZMSTEP DESC";

            using var cmd = new HanaCommand(sql, conn);
            cmd.Parameters.AddWithValue(":no", no);

            saleOrder = cmd.ExecuteScalar()?.ToString()?.TrimStart('0') ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HANA 查询失败: {ex.Message}");
        }

        var json = await GetPurchaseInfoAsync(no, lineno, supplier, lang);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        List<EWMLabelModel>? list;
        try
        {
            list = JsonConvert.DeserializeObject<List<EWMLabelModel>>(json);
        }
        catch
        {
            return null;
        }

        if (list == null || list.Count <= 0)
            return null;

        var rtlist = new List<EWMLabelModel>();
        var realdate = Config["deliveryDateState"];

        foreach (var model in list)
        {
            model.orderNo = PrinterHelper.ClearFrontZreo(model.orderNo);
            model.orderLineNo = PrinterHelper.ClearFrontZreo(model.orderLineNo);
            model.materialCode = PrinterHelper.ClearFrontZreo(model.materialCode);
            model.SalesOrderNo = saleOrder;
            model.SalesOrderNolineNO = model.orderLineNo;

            if (realdate == "real")
                model.OrderDelivery = model.Real_deliveryDate;

            model.supplier = PrinterHelper.ClearFrontZreo(model.supplier);
            rtlist.Add(model);
        }

        return rtlist;
    }

    #endregion

    #region Tab2 物料打印 — 查询

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
            return string.Empty;

        return resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty;
    }

    const string MaterialStockSql =
        "select IFNULL(aa.werks, bb.werks) as werks, " +
        "IFNULL(aa.MATNR, bb.MATNR) as MATNR, " +
        "IFNULL(aa.LABST, 0) + IFNULL(bb.LABST, 0) as LABST, " +
        "ifnull(aa.CHARG, bb.CHARG) as CHARG, VBELN, POSNR ,aa.LGORT " +
        "from (select werks, MATNR, CHARG, SUM(CLABS) as LABST ,LGORT " +
        "      from NSDM_V_MCHB where CLABS > 0 " +
        "      GROUP BY werks,MATNR, CHARG,LGORT) aa " +
        "full join (select werks,MATNR,SUM(KALAB) as LABST,CHARG, " +
        "                  ltrim(VBELN,'0') as VBELN,ltrim(POSNR,'0') as POSNR " +
        "           from NSDM_V_MSKA where KALAB > 0 " +
        "           GROUP BY werks,MATNR,CHARG,ltrim(VBELN,'0'),ltrim(POSNR,'0')) bb " +
        "on aa.werks = bb.werks and aa.MATNR = bb.MATNR  and aa.CHARG = bb.CHARG " +
        "where aa.werks=:werks AND ltrim(IFNULL(aa.MATNR,bb.MATNR),'0')=:material " +
        "order by aa.LGORT asc";

    List<EWMLabelModel> BuildMaterialStockRows(SAPMaterialModel materialModel, DataTable d1t)
    {
        var rtlist = new List<EWMLabelModel>();
        foreach (DataRow drr in d1t.Rows)
        {
            rtlist.Add(new EWMLabelModel
            {
                materialGroup = materialModel.MGroup,
                materialName = materialModel.Mname,
                materialCode = materialModel.newCode,
                materialModel_zh = materialModel.Mmodel,
                orderNo = "0000000000",
                orderLineNo = "00",
                orderQuantity = "999999",
                SalesOrderNo = drr["VBELN"].ToString(),
                SalesOrderNolineNO = drr["POSNR"].ToString(),
                client = drr["CHARG"].ToString(),
                rknum = drr["LABST"].ToString(),
                var2 = drr["LGORT"].ToString()
            });
        }

        return rtlist;
    }

    List<EWMLabelModel> BuildMaterialStockRowsForPo(
        SAPMaterialModel materialModel,
        SAPMaterialModel materialModelZh,
        SAPMaterialModel materialModelEn,
        DataTable d1t)
    {
        var rtlist = new List<EWMLabelModel>();
        foreach (DataRow drr in d1t.Rows)
        {
            rtlist.Add(new EWMLabelModel
            {
                materialGroup = materialModel.MGroup,
                materialName = materialModel.Mname,
                materialCode = materialModel.newCode,
                materialModel_zh = materialModelZh.Mmodel,
                materialModel_en = materialModelEn.Mmodel,
                orderNo = PrintPurchOrderNo,
                orderLineNo = PrintPurchLineNo,
                orderQuantity = "999999",
                SalesOrderNo = drr["VBELN"].ToString(),
                SalesOrderNolineNO = drr["POSNR"].ToString(),
                client = drr["CHARG"].ToString(),
                rknum = drr["LABST"].ToString(),
                var2 = drr["LGORT"].ToString()
            });
        }

        return rtlist;
    }

    Task<DataTable> QueryMaterialStockTableAsync(string factory, string material) =>
        Task.Run(() =>
        {
            using var conn = new HanaConnection(GetHanaConnectionString());
            conn.Open();
            using var cmd = new HanaCommand(MaterialStockSql, conn);
            cmd.Parameters.AddWithValue(":werks", factory);
            cmd.Parameters.AddWithValue(":material", material);
            using var dr = (HanaDataReader)cmd.ExecuteReader();
            return PrinterHelper.ConvertDataReaderToDataTable(dr);
        });

    /// <summary>对齐 WinForm <c>GetEWMLabelModelMaterial</c>。</summary>
    async Task<List<EWMLabelModel>?> GetEWMLabelModelMaterialAsync(string material, string lang)
    {
        try
        {
            var jsondtr = await GetMaterialModelLangAsync("", material, lang);
            if (jsondtr == "")
                return null;

            var materialModel = PrinterHelper.JsonToObject<SAPMaterialModel>(jsondtr);
            var rtlist = new List<EWMLabelModel>();

            try
            {
                if (PrintIsStock)
                {
                    var d1t = await QueryMaterialStockTableAsync(PrintFactory, material);
                    if (d1t.Rows.Count > 0)
                        rtlist = BuildMaterialStockRows(materialModel, d1t);

                    return rtlist;
                }

                rtlist.Add(new EWMLabelModel
                {
                    materialGroup = materialModel.MGroup,
                    materialName = materialModel.Mname,
                    materialCode = materialModel.newCode,
                    materialModel_zh = materialModel.Mmodel,
                    orderNo = "0000000000",
                    orderLineNo = "00",
                    orderQuantity = "999999",
                    SalesOrderNo = PrintSalesOrderNo,
                    SalesOrderNolineNO = PrintSalesLineNo
                });

                return rtlist;
            }
            catch (Exception ex)
            {
                SnackbarHelper.Show($"查询报错：{ex.Message}", Severity.Error);
                AppendLog($"物料查询 HANA 异常：{ex.Message}");
                return rtlist;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>对齐 WinForm <c>GetEWMLabelModelPo</c>（is_malaysia2 勾选）。</summary>
    async Task<List<EWMLabelModel>?> GetEWMLabelModelPoAsync(string material, string lang)
    {
        var saleOrder = string.Empty;

        using (var conn = new HanaConnection(GetHanaConnectionString()))
        {
            conn.Open();

            const string sql = "SELECT ZDJBH FROM ZTSD001_LOG " +
                               "WHERE ZTYPE = 'S' AND ZFLAG = 'C' AND ZYDJ = :po ORDER BY ZMSTEP DESC";

            using var cmd = new HanaCommand(sql, conn);
            cmd.Parameters.AddWithValue(":po", PrintPurchOrderNo);
            saleOrder = (cmd.ExecuteScalar()?.ToString() ?? string.Empty).TrimStart('0');
        }

        try
        {
            var jsondtr = await GetMaterialModelLangAsync("", material, lang);
            if (jsondtr == "")
                return null;

            var materialModel = PrinterHelper.JsonToObject<SAPMaterialModel>(jsondtr);

            var jsonEn = await GetMaterialModelLangAsync("", material, "E");
            if (jsonEn == "")
                return null;
            var materialModelEn = PrinterHelper.JsonToObject<SAPMaterialModel>(jsonEn);

            var jsonZh = await GetMaterialModelLangAsync("", material, "1");
            if (jsonZh == "")
                return null;
            var materialModelZh = PrinterHelper.JsonToObject<SAPMaterialModel>(jsonZh);

            var rtlist = new List<EWMLabelModel>();

            try
            {
                if (PrintIsStock)
                {
                    var d1t = await QueryMaterialStockTableAsync(PrintFactory, material);
                    if (d1t.Rows.Count > 0)
                        rtlist = BuildMaterialStockRowsForPo(materialModel, materialModelZh, materialModelEn, d1t);

                    return rtlist;
                }

                var esmodel = new EWMLabelModel
                {
                    materialGroup = materialModel.MGroup,
                    materialName = materialModel.Mname,
                    materialCode = materialModel.newCode,
                    materialModel_zh = materialModelZh.Mmodel,
                    materialModel_en = materialModelEn.Mmodel,
                    orderNo = PrintPurchOrderNo,
                    orderLineNo = PrintPurchLineNo,
                    orderQuantity = "999999",
                    SalesOrderNo = saleOrder,
                    SalesOrderNolineNO = PrintPurchLineNo
                };
                rtlist.Add(esmodel);

                // 对齐 WinForm：回写销单号/行号到界面
                PrintSalesOrderNo = esmodel.SalesOrderNo;
                PrintSalesLineNo = esmodel.SalesOrderNolineNO;

                return rtlist;
            }
            catch (Exception ex)
            {
                SnackbarHelper.Show($"查询报错：{ex.Message}", Severity.Error);
                AppendLog($"物料 Po 模式 HANA 异常：{ex.Message}");
                return rtlist;
            }
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Tab3 销单库存 — 查询

    /// <summary>对齐 WinForm <c>GetDetailBySaleOrderNo</c>：GetSaleInfoCM_Lang。</summary>
    async Task<string> GetSaleInfoCM_LangJsonAsync(string so, string lang)
    {
        var resp = await SapEwmService.InvokeAsync(
            "GetSaleInfoCM_Lang",
            new Dictionary<string, string?> { ["so"] = so, ["lang"] = lang },
            logToBrowserConsole: true);

        if (resp.Code != ApiResultCode.Success || resp.Data is null)
            return string.Empty;

        return resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty;
    }

    /// <summary>对齐 WinForm <c>GetDetailBySaleOrderNo</c>。</summary>
    async Task<List<EWMLabelModel>?> GetDetailBySaleOrderNoAsync(string so, string lang)
    {
        var json = await GetSaleInfoCM_LangJsonAsync(so, lang);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return PrinterHelper.JsonToObject<List<EWMLabelModel>>(json);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Tab4 交货订单 — 查询

    /// <summary>对齐 WinForm <c>GetEWMLabelModelJH</c>。</summary>
    Task<List<EWMLabelModel>> GetEWMLabelModelJHAsync(string no) =>
        Task.Run(() =>
        {
            var rtlist = new List<EWMLabelModel>();

            try
            {
                using var conn = new HanaConnection(GetHanaConnectionString());
                conn.Open();

                const string sql =
                    "Select ltrim(LIPS.VBELN,'0') as VBELN," +
                    "       ltrim(LIPS.POSNR,'0') as POSNR," +
                    "       ltrim(LIKP.LIFNR,'0') as LIFNR," +
                    "       LIPS.VGBEL," +
                    "       ltrim(LIPS.VGPOS,'0') as VGPOS," +
                    "       ltrim(LIPS.MATNR,'0') as MATNR," +
                    "       LIPS.ARKTX," +
                    "       LIPS.LFIMG," +
                    "       LIPS.CHARG," +
                    "       LIPS.VBELV," +
                    "       LIPS.POSNV," +
                    "       LIPS.WERKS," +
                    "       LIPS.LGORT," +
                    "       ltrim(LIPS.KDAUF,'0') as KDAUF," +
                    "       ltrim(LIPS.KDPOS,'0') as KDPOS " +
                    "from   LIPS " +
                    "left join LIKP on LIPS.VBELN = LIKP.VBELN " +
                    "where  ltrim(LIPS.VBELN, '0') = :no " +
                    "order by LIPS.VBELV, LIPS.POSNV";

                using var cmd = new HanaCommand(sql, conn);
                cmd.Parameters.AddWithValue(":no", no);
                using var dr = cmd.ExecuteReader();
                var d1t = PrinterHelper.ConvertDataReaderToDataTable(dr);

                if (d1t.Rows.Count > 0)
                {
                    foreach (DataRow drr in d1t.Rows)
                    {
                        var kdpau = drr["KDAUF"].ToString();
                        var kdpos = drr["KDPOS"].ToString();
                        rtlist.Add(new EWMLabelModel
                        {
                            orderNo = drr["VGBEL"].ToString(),
                            orderLineNo = drr["VGPOS"].ToString(),
                            materialCode = drr["MATNR"].ToString(),
                            materialName = drr["ARKTX"].ToString(),
                            orderQuantity = drr["LFIMG"].ToString(),
                            SalesOrderNo = !string.IsNullOrEmpty(kdpau) ? kdpau : drr["VBELV"].ToString(),
                            SalesOrderNolineNO = !string.IsNullOrEmpty(kdpos) ? kdpos : drr["POSNV"].ToString(),
                            factory = drr["WERKS"].ToString(),
                            materialModel_zh = string.Empty,
                            supplier = drr["LIFNR"] + " " + drr["WERKS"] + " " + drr["LGORT"],
                            batchNo = drr["CHARG"].ToString(),
                            jclogoType = drr["VBELN"].ToString(),
                            jclogoText = drr["POSNR"].ToString()
                        });
                    }

                    return rtlist;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"交货单查询报错：{ex}");
            }

            return rtlist;
        });

    #endregion

    #region Tab0 生产入库 — 查询入口

    async Task SearchSubmit()
    {
        if (IsSearching) return;

        if (string.IsNullOrWhiteSpace(ProdRemark) && string.IsNullOrWhiteSpace(ProdOrderNo))
        {
            SnackbarHelper.Show("请输入生产订单号或销售订单", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProdOrderNo))
            return;

        IsSearching = true;
        ResetSearchState();
        BoxNum = string.Empty;
        SumNum = string.Empty;
        await InvokeAsync(StateHasChanged);

        try
        {
            var productionNo = ProdOrderNo.Trim();
            var lang = GetLangParam();
            var model = await GetEWMLabelModelSCAsync(productionNo, lang);

            if (string.IsNullOrEmpty(model.orderNo))
            {
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            LSP = await GetBatchAsync();
            printModel = model;
            printModelList = new List<EWMLabelModel> { printModel };
            await TryLoadCustomerPoForProductionOrderAsync(productionNo);
            AppendLog($"查询成功：{productionNo}");
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

    #endregion

    #region Tab1 采购收货 — 查询入口

    async Task SearchSubmit2()
    {
        if (IsSearching) return;
        if (string.IsNullOrEmpty(PurchOrderNo) || string.IsNullOrEmpty(PurchLineNo))
        {
            SnackbarHelper.Show("请输入采购订单", Severity.Warning);
            return;
        }

        IsSearching = true;
        ResetSearchState();
        await InvokeAsync(StateHasChanged);

        try
        {
            var po = PurchOrderNo.Trim();
            var poline = PurchLineNo.Trim();
            var lang = GetLangParam();
            var supplier = Config["supplierCode"] ?? string.Empty;
            var list = await GetEWMLabelModelCGAsync(po, poline, supplier, lang);

            if (list == null)
            {
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            LSP = await GetBatchAsync();
            printModelList = list;
            printModel = list[0];
            AppendLog($"采购查询成功：{po}/{poline}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"采购查询失败：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Tab2 物料打印 — 查询入口

    async Task SearchSubmit3()
    {
        if (IsSearching) return;

        IsSearching = true;
        ResetSearchState();
        await InvokeAsync(StateHasChanged);

        try
        {
            var lang = GetLangParam();
            var materialno = PrintMaterialNo?.Trim() ?? string.Empty;
            List<EWMLabelModel>? list;

            if (PrintIsMalaysia2)
            {
                list = await GetEWMLabelModelPoAsync(materialno, lang);
            }
            else
            {
                if (string.IsNullOrEmpty(PrintMaterialNo))
                {
                    SnackbarHelper.Show("请输入物料号", Severity.Warning);
                    return;
                }

                list = await GetEWMLabelModelMaterialAsync(materialno, lang);
            }

            if (list == null || list.Count == 0)
            {
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            printModelList = list;
            printModel = list[0];
            BoxNum = "0";
            SumNum = "0";
            AppendLog($"物料查询成功：{materialno}");
            SnackbarHelper.Show("查询成功", Severity.Success);

            LSP = await GetBatchAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"物料查询失败：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Tab3 销单库存 — 查询入口

    async Task SearchSubmit4()
    {
        if (IsSearching) return;
        if (string.IsNullOrEmpty(SalesOrderNo))
            return;

        IsSearching = true;
        ResetSearchState();
        await InvokeAsync(StateHasChanged);

        try
        {
            var lang = GetLangParam();
            var list = await GetDetailBySaleOrderNoAsync(SalesOrderNo, lang);

            if (list == null)
            {
                SnackbarHelper.Show("未找到信息", Severity.Warning);
                return;
            }

            printModelList = list;
            printModel = list[0];
            AppendLog($"销售订单查询成功：{SalesOrderNo}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"销售订单查询失败：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Tab4 交货订单 — 查询入口

    async Task SearchSubmit5()
    {
        if (IsSearching) return;
        if (string.IsNullOrEmpty(StockDeliveryOrderNo))
        {
            SnackbarHelper.Show("请输入交货订单", Severity.Warning);
            return;
        }

        IsSearching = true;
        ResetSearchState();
        await InvokeAsync(StateHasChanged);

        try
        {
            var list = await GetEWMLabelModelJHAsync(StockDeliveryOrderNo);

            if (list == null)
            {
                SnackbarHelper.Show("未找到产品信息", Severity.Warning);
                return;
            }

            if (list.Count == 0)
            {
                SnackbarHelper.Show("未找到订单信息", Severity.Warning);
                return;
            }

            LSP = await GetBatchAsync();
            printModelList = list;
            printModel = list[0];
            AppendLog($"交货单查询成功：{StockDeliveryOrderNo}");
            SnackbarHelper.Show("查询成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"交货单查询失败：{ex.Message}");
            SnackbarHelper.Show($"查询失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    void OnRowClicked(TableRowClickEventArgs<EWMLabelModel> args)
    {
        if (args.Item == null)
            return;

        SumNum = args.Item.rknum ?? string.Empty;
        printModel = args.Item;
        InvokeAsync(StateHasChanged);
    }

    string GetTableRowClass(EWMLabelModel row, int rowIndex) =>
        printModel != null && ReferenceEquals(row, printModel) ? "material2-selected-row" : string.Empty;

    #endregion

    #region 打印 — 公共

    bool IsProductionTab() => LogicalTab == 0;

    string GetTabRemark() =>
        IsProductionTab() ? ProdRemark : PrintRemark;

    (bool ok, float w, float h) TryParsePaper(string paper)
    {
        try
        {
            var parts = (paper ?? "").Split(new[] { '*', 'x', 'X' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return (false, 0, 0);
            return (true, float.Parse(parts[0]), float.Parse(parts[1]));
        }
        catch
        {
            return (false, 0, 0);
        }
    }

    async Task<string> GetHUAsync(string factory, string guid)
    {
        try
        {
            var resp = await SapEwmService.InvokeAsync(
                "GetHU",
                new Dictionary<string, string?>
                {
                    ["factory"] = factory,
                    ["guid"] = guid
                },
                logToBrowserConsole: false);

            if (resp.Code != ApiResultCode.Success || resp.Data is null)
                return HuPlaceholder;

            return NormalizeHuValue(resp.Data.ResultText ?? resp.Data.JsonResult ?? string.Empty);
        }
        catch (Exception ex)
        {
            AppendLog("GetHU异常，使用占位HU：" + ex.Message);
            return HuPlaceholder;
        }
    }

    /// <summary>仅允许合法 HU 进二维码；空/JSON 报错等一律回退占位符（对齐 WinForm NormalizeHuValue）。</summary>
    static string NormalizeHuValue(string? hu)
    {
        if (string.IsNullOrWhiteSpace(hu))
            return HuPlaceholder;

        var value = hu.Trim();
        if (value.Contains('{', StringComparison.Ordinal)
            || value.Contains('}', StringComparison.Ordinal)
            || value.Contains("\"TYPE\"", StringComparison.OrdinalIgnoreCase)
            || value.Contains("\"MESSAGE\"", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Invalid request", StringComparison.OrdinalIgnoreCase)
            || value.Contains('/'))
            return HuPlaceholder;

        return value;
    }

    bool IsSmtMode() =>
        string.Equals(LaunchType, "smt", StringComparison.OrdinalIgnoreCase);

    /// <summary>V2：type=smt 且当前为生产入库 / 采购收货 / 物料打印。</summary>
    bool UseProductInboundQrV2() =>
        IsSmtMode() && LogicalTab is 0 or 1 or 2;

    string BuildProductInboundQrValueV2(
        string? orderNo,
        string? orderLineNo,
        string? materialCode,
        string? batchQuantity,
        string? uniqueCode,
        string? salesOrderNo,
        string? salesOrderLineNo,
        string? batch,
        string? trayQuantity,
        bool hideBatch = false) =>
        string.Join("/",
            orderNo ?? string.Empty,
            orderLineNo ?? string.Empty,
            materialCode ?? string.Empty,
            batchQuantity ?? string.Empty,
            batchQuantity ?? string.Empty,
            uniqueCode ?? string.Empty,
            salesOrderNo ?? string.Empty,
            salesOrderLineNo ?? string.Empty,
            "BATCH",
            hideBatch ? string.Empty : (batch ?? string.Empty),
            trayQuantity ?? string.Empty);

    async Task<string> BuildProductInboundQrValueV2FromModelAsync(string? batch, bool hideBatch = false)
    {
        if (printModel is null)
            throw new InvalidOperationException("printModel 为空，无法生成新版二维码。");

        var uniqueCode = await NextLabelUniqueCodeV2Async();
        _currentLabelUniqueCodeV2 = uniqueCode;

        return BuildProductInboundQrValueV2(
            printModel.orderNo,
            printModel.orderLineNo,
            printModel.materialCode,
            printModel.amountQuantity,
            uniqueCode,
            printModel.SalesOrderNo,
            printModel.SalesOrderNolineNO,
            batch,
            printModel.boxQuantity,
            hideBatch);
    }

    /// <summary>从 JClogin.LabelUniqueCodeSeq 取号：S + yyMMdd + 5 位流水。</summary>
    async Task<string> NextLabelUniqueCodeV2Async()
    {
        var connStr = Config.GetConnectionString("JClogin_connStr");
        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("未配置 ConnectionStrings:JClogin_connStr。");

        var bizDate = DateTime.Today;
        const string sql = @"
MERGE dbo.LabelUniqueCodeSeq WITH (HOLDLOCK) AS t
USING (SELECT CAST(@BizDate AS DATE) AS BizDate) AS s
ON t.BizDate = s.BizDate
WHEN MATCHED THEN
    UPDATE SET CurrentSeq = t.CurrentSeq + 1, UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (BizDate, CurrentSeq, UpdatedAt) VALUES (s.BizDate, 1, GETDATE())
OUTPUT INSERTED.CurrentSeq;";

        await using var conn = new SqlConnection(connStr);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@BizDate", SqlDbType.Date).Value = bizDate;
        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        if (result is null or DBNull)
            throw new InvalidOperationException("获取标签唯一码流水号失败。");

        var seq = Convert.ToInt32(result);
        if (seq is < 1 or > 99999)
            throw new InvalidOperationException("当天唯一码流水号已超出 5 位上限(99999)。");

        return "S" + bizDate.ToString("yyMMdd") + seq.ToString("00000");
    }

    /// <summary>80×60 / 60×40 数量行；V2 时拼总数与唯一码。</summary>
    string FormatSmallLabelQuantityPcsText()
    {
        var qty = printModel?.boxQuantity ?? BoxNum ?? string.Empty;
        if (!UseProductInboundQrV2())
            return qty + "   Pcs";

        var total = printModel?.amountQuantity ?? SumNum ?? string.Empty;
        var uid = _currentLabelUniqueCodeV2 ?? string.Empty;
        if (IsChineseLang())
            return qty + " Pcs | " + total + " 总数 | " + uid;
        return qty + " Pcs | " + total + " SUM| " + uid;
    }

    bool IsBilingualPrintMode() =>
        string.Equals(SpecialPrint, "双语打印", StringComparison.Ordinal);

    bool ValidateSpecialPrintMode()
    {
        if (string.Equals(SpecialPrint, "无", StringComparison.Ordinal)
            || IsBilingualPrintMode()
            || PrintIsMalaysia
            || PrintIsMalaysia2)
            return true;

        SnackbarHelper.Show("当前特殊打印模式不可用", Severity.Warning);
        return false;
    }

    /// <summary>双语：字段名双行中英 + 缩小字号（对齐 WinForm is_2L）。</summary>
    string FieldCaption(string zhKey, string enFallback)
    {
        if (!IsBilingualPrintMode())
            return T[zhKey];
        // 与 WinForm 固定中英双行一致，避免资源串已是单语时对不齐
        return zhKey switch
        {
            "Supplier" => "供应商\nSupplier",
            "Order&Row" => "订单&行\nOrder&row",
            "SaleNo&Row" => "销单&行\nSaleNo.&row",
            "Material" => "物料\nMaterial",
            "MaterialDes." => "物料描述\nMaterial Des.",
            "Model" => "规格型号\nModel",
            "Quantity" => "数量\nQuantity",
            "TotalQuantity" => "总数量\nTotalQuantity",
            _ => T[zhKey] + "\n" + enFallback
        };
    }

    float FieldCaptionFontSize(float normal) =>
        IsBilingualPrintMode() ? Math.Min(normal, 8f) : normal;

    /// <summary>小标签字段名：双语固定中英双行；否则按界面语言单行（对齐 WinForm 60×40/80×60）。</summary>
    string SmallFieldCaption(string role)
    {
        if (IsBilingualPrintMode())
        {
            return role switch
            {
                "supplier" => "供货组织\nSupplier",
                "order" => "单号行号\nOrder&row",
                "material" => "物料编号\nMaterial",
                "name" => "物料名称\nMa Des.",
                "model" => "规格型号\nModel",
                "qty" => "数量\nQuantity",
                _ => role
            };
        }

        var zh = IsChineseLang();
        return role switch
        {
            "supplier" => zh ? "供货组织" : "Supplier",
            "order" => zh ? "单号行号" : "Order&row",
            "material" => zh ? "物料编号" : "Material",
            "name" => zh ? "物料名称" : "Ma Des.",
            "model" => zh ? "规格型号" : "Model",
            "qty" => zh ? "数量" : "Quantity",
            _ => role
        };
    }

    float SmallFieldCaptionFontSize(float paperDefault) =>
        IsBilingualPrintMode()
            ? (string.Equals(PaperSize, "60*40", StringComparison.Ordinal) ? 5f : 7f)
            : paperDefault;

    /// <summary>小标签工厂/行号格（对齐 WinForm grid9）。</summary>
    string BuildSmallLabelFactoryOrLineNo()
    {
        if (LogicalTab is 1 or 4)
            return printModel!.orderLineNo ?? string.Empty;

        if (LogicalTab == 0 && !string.IsNullOrEmpty(location_code))
            return $"{printModel!.factory}->{location_code}";

        return printModel!.factory ?? string.Empty;
    }

    async Task<bool> PreparePrintDataAsync() => LogicalTab switch
    {
        0 => await Production_GenerateAsync(),
        1 => await Purchase_GenerateAsync(),
        2 => await Material_GenerateAsync(),
        3 => await SalesStock_GenerateAsync(),
        4 => await Delivery_GenerateAsync(),
        _ => false
    };

    (List<PrintBlock>? blocks, float borderThickness) GetPrintBlocksForActiveTab(string paperSize) =>
        LogicalTab switch
        {
            0 => GetProductionPrintBlocks(paperSize),
            1 => GetPurchasePrintBlocks(paperSize),
            2 => GetMaterialPrintBlocks(paperSize),
            3 => GetSalesStockPrintBlocks(paperSize),
            4 => GetDeliveryPrintBlocks(paperSize),
            _ => (null, 0.2f)
        };

    (List<PrintBlock>? blocks, float borderThickness) GetProductionPrintBlocks(string paperSize) => paperSize switch
    {
        "80*60" => (Production_Print80x60(), 0.3f),
        "100*80" => ResolvePrint100x80(0),
        "100*120" => (Production_Print100x120(), 0.4f),
        "60*40" => (Production_Print60x40(), 0.3f),
        "297*210" => (BuildA4UsLabelPrintBlocks(), 0f),
        _ => (null, 0.2f)
    };

    (List<PrintBlock>? blocks, float borderThickness) GetPurchasePrintBlocks(string paperSize) => paperSize switch
    {
        "80*60" => (Purchase_Print80x60(), 0.3f),
        "100*80" => ResolvePrint100x80(1),
        "100*120" => (Purchase_Print100x120(), 0.4f),
        "60*40" => (Purchase_Print60x40(), 0.3f),
        "297*210" => (BuildA4UsLabelPrintBlocks(), 0f),
        _ => (null, 0.2f)
    };

    (List<PrintBlock>? blocks, float borderThickness) GetMaterialPrintBlocks(string paperSize) => paperSize switch
    {
        "80*60" => (Material_Print80x60(), 0.3f),
        "100*80" => ResolvePrint100x80(2),
        "100*120" => (Material_Print100x120(), 0.4f),
        "60*40" => (Material_Print60x40(), 0.3f),
        "297*210" => (BuildA4UsLabelPrintBlocks(), 0f),
        _ => (null, 0.2f)
    };

    (List<PrintBlock>? blocks, float borderThickness) GetSalesStockPrintBlocks(string paperSize) => paperSize switch
    {
        "80*60" => (SalesStock_Print80x60(), 0.3f),
        "100*80" => ResolvePrint100x80(3),
        "100*120" => (SalesStock_Print100x120(), 0.4f),
        "60*40" => (SalesStock_Print60x40(), 0.3f),
        "297*210" => (BuildA4UsLabelPrintBlocks(), 0f),
        _ => (null, 0.2f)
    };

    (List<PrintBlock>? blocks, float borderThickness) GetDeliveryPrintBlocks(string paperSize) => paperSize switch
    {
        "80*60" => (Delivery_Print80x60(), 0.3f),
        "100*80" => ResolvePrint100x80(4),
        "100*120" => (Delivery_Print100x120(), 0.4f),
        "60*40" => (Delivery_Print60x40(), 0.3f),
        "297*210" => (BuildA4UsLabelPrintBlocks(), 0f),
        _ => (null, 0.2f)
    };

    /// <summary>
    /// A4 美国标签：设计 297×210，打印强制旋转 90°（对齐 WinForm DrawA4UsLabelPage）。
    /// 仅物料号 / 数量 EA / BN.批次 / 右上二维码。
    /// </summary>
    List<PrintBlock> BuildA4UsLabelPrintBlocks()
    {
        var qty = (printModel?.boxQuantity ?? BoxNum ?? string.Empty).Trim();
        var qtyText = string.IsNullOrEmpty(qty) ? string.Empty : qty + " EA";
        var batchDisplay = GetBatchForDisplay();
        var batchText = string.IsNullOrEmpty(batchDisplay) ? "BN. " : "BN. " + batchDisplay;

        return new List<PrintBlock>
        {
            new()
            {
                Name = "A4 Material", X = 6, Y = 37, Width = 287, Height = 53, ShowBorder = false,
                Text = new TextElement
                {
                    Content = printModel?.materialCode ?? string.Empty,
                    FontName = "Arial",
                    FontSize = 100,
                    Bold = true,
                    Align = "center",
                    VerticalAlign = "middle"
                }
            },
            new()
            {
                Name = "A4 Qty", X = 5, Y = 91, Width = 288, Height = 53, ShowBorder = false,
                Text = new TextElement
                {
                    Content = qtyText,
                    FontName = "Arial",
                    FontSize = 100,
                    Bold = true,
                    Align = "center",
                    VerticalAlign = "middle"
                }
            },
            new()
            {
                Name = "A4 Batch", X = 6, Y = 144, Width = 287, Height = 53, ShowBorder = false,
                Text = new TextElement
                {
                    Content = batchText,
                    FontName = "Arial",
                    FontSize = 90,
                    Bold = true,
                    Align = "center",
                    VerticalAlign = "middle"
                }
            },
            new()
            {
                Name = "A4 QR", X = 245, Y = 5, Width = 35, Height = 35, ShowBorder = false,
                Qr = new QrElement
                {
                    Value = QrValue ?? string.Empty,
                    ErrorCorrection = "M",
                    Charset = "UTF-8",
                    SafeMarginMm = 1f
                }
            }
        };
    }

    (List<PrintBlock> blocks, float borderThickness) ResolvePrint100x80(int tabIndex)
    {
        if (PrintIsMalaysia)
            return (BuildMalaysiaMasPrint100x80(tabIndex), 0.2f);
        if (PrintIsMalaysia2)
            return (BuildMalaysia2Print100x80(), 0.4f);
        return (BuildTabPrint100x80(tabIndex), 0.4f);
    }

    #endregion

    #region 打印 — 分发

    async Task Generate()
    {
        await PreparePrintDataAsync();
    }

    async Task PrintSubmit()
    {
        if (IsPrinting) return;

        if (!ValidateSpecialPrintMode())
            return;

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            SnackbarHelper.Show("请先选择打印机", Severity.Warning);
            return;
        }

        var (ok, w, h) = TryParsePaper(PaperSize);
        if (!ok || w <= 0 || h <= 0)
        {
            SnackbarHelper.Show("纸张尺寸格式错误（如 100*80）", Severity.Error);
            return;
        }

        if (!int.TryParse(PrintCount, out var count) || count < 1)
        {
            SnackbarHelper.Show("打印数量无效", Severity.Error);
            return;
        }

        if (printModel is null)
        {
            SnackbarHelper.Show("暂无可打印的数据，请先查询", Severity.Warning);
            return;
        }

        if (!ValidateWeightBeforePrint())
            return;

        IsPrinting = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (!await PreparePrintDataAsync())
                return;

            if (string.IsNullOrEmpty(QrValue))
                return;

            var (list, borderThickness) = GetPrintBlocksForActiveTab(PaperSize);
            if (list is null)
            {
                SnackbarHelper.Show("当前 Tab 或纸张规格的标签尚未实现", Severity.Warning);
                return;
            }

            // A4 美国标签：设计 297×210，强制旋转 90°（对齐 WinForm）
            var forceRotate = string.Equals(PaperSize, "297*210", StringComparison.Ordinal)
                              || if_rotate;

            var layout = new PrintLayoutModel
            {
                PrinterName = SelectedPrinter,
                PaperWidthMm = w,
                PaperHeightMm = h,
                Copies = 1,
                StartValue = 1,
                PrintCount = count,
                GapMm = null,
                BorderThicknessMm = borderThickness,
                Blocks = list,
                IsRotate = forceRotate,
            };

            AppendLog($"发送打印：Tab={LogicalTab} | {SelectedPrinter} | {w}×{h}mm | 数量={count}");

            await PrinterHelper.PrintLayoutAsync(JS, layout);
            await SavePrintSettingsAsync();
            AppendLog("打印提交成功");

            if (ShouldPrintTempBoxLabels())
                await PrintTempBoxExtraLabelsAsync(count);

            SnackbarHelper.Show("打印提交成功", Severity.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"打印异常：{ex.Message}");
            SnackbarHelper.Show($"打印异常：{ex.Message}", Severity.Error);
        }
        finally
        {
            IsPrinting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>对齐 WinForm <c>PrintTempBoxExtraLabels</c>：发国外物料 HU 配套标签。</summary>
    async Task PrintTempBoxExtraLabelsAsync(int printQty)
    {
        if (printModel is null)
            return;

        var productionOrderNo = printModel.orderNo;
        if (string.IsNullOrWhiteSpace(productionOrderNo) && !string.IsNullOrWhiteSpace(ProdOrderNo))
            productionOrderNo = ProdOrderNo.Trim();

        if (string.IsNullOrWhiteSpace(productionOrderNo))
        {
            SnackbarHelper.Show("海外配套标签：生产订单号为空", Severity.Warning);
            return;
        }

        List<TempLabelItem> tempItems;
        try
        {
            tempItems = await LoadTempLabelItemsAsync(productionOrderNo);
        }
        catch (Exception ex)
        {
            AppendLog($"海外配套标签 HANA 查询失败：{ex.Message}");
            SnackbarHelper.Show($"海外配套标签 HANA 查询失败：{ex.Message}", Severity.Error);
            return;
        }

        if (tempItems.Count == 0)
        {
            AppendLog("海外配套标签：未找到需打印的额外标签");
            return;
        }

        var batchForTemp = batchNo;
        if (string.IsNullOrEmpty(batchForTemp))
        {
            SnackbarHelper.Show("海外配套标签：批次号为空", Severity.Warning);
            return;
        }

        const float huPaperW = 100f;
        const float huPaperH = 80f;
        var qtyText = !string.IsNullOrWhiteSpace(BoxNum) ? BoxNum.Trim() : "1";

        foreach (var item in tempItems)
        {
            var qrValue = BuildTempLabelQrValue(item.MaterialCode, batchForTemp);
            AppendLog($"海外配套标签：{qrValue}");

            var blocks = BuildHuTempPrintBlocks(
                item.MaterialCode,
                item.MaterialDesc,
                batchForTemp,
                qrValue,
                qtyText,
                _customerPoNoForLabel);

            var layout = new PrintLayoutModel
            {
                PrinterName = SelectedPrinter,
                PaperWidthMm = huPaperW,
                PaperHeightMm = huPaperH,
                Copies = 1,
                StartValue = 1,
                PrintCount = printQty,
                GapMm = null,
                BorderThicknessMm = 0.3f,
                Blocks = blocks,
                IsRotate = false
            };

            await PrinterHelper.PrintLayoutAsync(JS, layout);
        }

        AppendLog($"海外配套标签已提交：{tempItems.Count} 项");
    }

    /// <summary>对齐 WinForm LabelPrintForHu / DrawHuTempTemplate（100×80 mm，blocks 网格）。</summary>
    List<PrintBlock> BuildHuTempPrintBlocks(
        string materialCode,
        string materialDesc,
        string batchNoValue,
        string qrValue,
        string qty,
        string customerPo)
    {
        const float xL = 2f;
        const float wL = 25f;
        const float xV1 = 27f;
        const float wV1 = 23f;
        const float xV2 = 50f;
        const float wV2 = 23f;
        const float xQr = 73f;
        const float wQr = 25f;
        const float rowH = 6f;
        const float y1 = 2f;
        const float y2 = 8f;
        const float y3 = 14f;
        const float y4 = 20f;
        const float y5 = 26f;
        const float y6 = 32f;
        const float y7 = 42f;
        const float y8 = 56f;
        const float y9 = 64f;
        const float y10 = 70f;

        const float huFontSize = 10f;

        PrintBlock HuCell(string name, float x, float y, float w, float h, string content) =>
            new()
            {
                Name = name,
                X = x,
                Y = y,
                Width = w,
                Height = h,
                ShowBorder = true,
                Text = new TextElement
                {
                    Content = content,
                    FontName = "Times New Roman",
                    FontSize = huFontSize,
                    Bold = false,
                    Align = "left",
                    VerticalAlign = "middle"
                }
            };

        return new List<PrintBlock>
        {
            HuCell("Supplier L", xL, y1, wL, rowH, "Supplier"),
            HuCell("Supplier V", xV1, y1, wV1 + wV2, rowH, string.Empty),
            HuCell("Supplier TR", xQr, y1, wQr, rowH, string.Empty),

            HuCell("Pur L", xL, y2, wL, rowH, "Pur.order/Item"),
            HuCell("Pur PO", xV1, y2, wV1, rowH, customerPo),
            HuCell("Pur Item", xV2, y2, wV2, rowH, string.Empty),

            HuCell("Sal L", xL, y3, wL, rowH, "Sal.order/Item"),
            HuCell("Sal No", xV1, y3, wV1, rowH, string.Empty),
            HuCell("Sal Item", xV2, y3, wV2, rowH, string.Empty),

            HuCell("Mat L", xL, y4, wL, rowH, "Mat./Batch"),
            HuCell("Mat No", xV1, y4, wV1, rowH, materialCode),
            HuCell("Batch", xV2, y4, wV2, rowH, batchNoValue),

            HuCell("Plant L", xL, y5, wL, rowH, "Plant/SLoc"),
            HuCell("Plant", xV1, y5, wV1, rowH, string.Empty),
            HuCell("SLoc", xV2, y5, wV2, rowH, string.Empty),

            new()
            {
                Name = "QR",
                X = xQr,
                Y = y2,
                Width = wQr,
                Height = y6 - y2,
                ShowBorder = true,
                Qr = new QrElement { Value = qrValue, ErrorCorrection = "M", Charset = "UTF-8", SafeMarginMm = 0.5f }
            },

            HuCell("Desc L", xL, y6, wL, y7 - y6, "Mat.Description"),
            HuCell("Desc V", xV1, y6, wV1 + wV2 + wQr, y7 - y6, materialDesc),

            HuCell("Spec L", xL, y7, wL, y8 - y7, "Specification"),
            HuCell("Spec V", xV1, y7, wV1 + wV2 + wQr, y8 - y7, string.Empty),

            HuCell("Qty L", xL, y8, wL, rowH + 2, "Qty/Pallet"),
            HuCell("Qty V", xV1, y8, 20f, rowH + 2, qty),
            HuCell("MPQ L", 47f, y8, 26f, rowH + 2, "MPQ"),
            HuCell("MPQ V", 73f, y8, wQr, rowH + 2, string.Empty),

            HuCell("Date L", xL, y9, wL, rowH, "Printing Date"),
            HuCell("Date V", xV1, y9, wV1 + wV2 + wQr, rowH, DateTime.Now.ToString("yyyy-MM-dd")),

            HuCell("Remark L", xL, y10, wL, rowH, "remark"),
            HuCell("Remark V", xV1, y10, wV1 + wV2 + wQr, rowH, string.Empty)
        };
    }

    #endregion

    #region Tab0 生产入库 — 打印 Blocks

    List<PrintBlock> Production_Print60x40()
    {
        var labelFs = SmallFieldCaptionFontSize(6);
        var valueFs = IsBilingualPrintMode() ? 6f : 6f;
        var batchDisplay = GetBatchForDisplay();
        return new List<PrintBlock>
        {
            new()
            {
                Name = "Block 1", X = 2, Y = 3, Width = 12, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("supplier"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 2", X = 14, Y = 3, Width = 29, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = printModel!.supplier, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 3", X = 43, Y = 3, Width = 15, Height = 15, ShowBorder = true,
                Qr = new QrElement { Value = QrValue ?? "", ErrorCorrection = "M", Charset = "UTF-8", SafeMarginMm = 0.5f }
            },
            new()
            {
                Name = "Block 4", X = 2, Y = 8, Width = 12, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("order"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 5", X = 14, Y = 8, Width = 14, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = printModel.orderNo, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 6", X = 28, Y = 8, Width = 15, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = BuildSmallLabelFactoryOrLineNo(), FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 7", X = 2, Y = 13, Width = 12, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("material"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 8", X = 14, Y = 13, Width = 14, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialCode, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 9", X = 28, Y = 13, Width = 15, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = batchDisplay, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 10", X = 2, Y = 18, Width = 12, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("name"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 11", X = 14, Y = 18, Width = 44, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialName, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 12", X = 2, Y = 23, Width = 12, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("model"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 13", X = 14, Y = 23, Width = 44, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialModel_zh, FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 14", X = 2, Y = 30, Width = 12, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("qty"), FontName = "Arial", FontSize = labelFs, Bold = true, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 15", X = 14, Y = 30, Width = 44, Height = 5, ShowBorder = true,
                Text = new TextElement { Content = FormatSmallLabelQuantityPcsText(), FontName = "Arial", FontSize = valueFs, Bold = false, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 16", X = 2, Y = 35, Width = 56, Height = 3, ShowBorder = true,
                Text = new TextElement
                {
                    Content = BuildSmallLabelDateTimeWithTime(),
                    FontName = "Arial", FontSize = 6, Bold = false, Align = "left", VerticalAlign = "middle"
                }
            }
        };
    }

    List<PrintBlock> Production_Print80x60()
    {
        var labelFs = SmallFieldCaptionFontSize(8f);
        var valueFs = IsBilingualPrintMode() ? 8f : 8f;
        var batchDisplay = GetBatchForDisplay();
        return new List<PrintBlock>
        {
            new()
            {
                Name = "供货组织", X = 2, Y = 2, Width = 16, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("supplier"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "supplier", X = 18, Y = 2, Width = 40, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel!.supplier, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "二维码", X = 58, Y = 2, Width = 20, Height = 21, ShowBorder = true,
                Qr = new QrElement { Value = QrValue ?? "", ErrorCorrection = "M", Charset = "UTF-8", SafeMarginMm = 0.5f }
            },
            new()
            {
                Name = "单号行号", X = 2, Y = 9, Width = 16, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("order"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "orderLineNo", X = 18, Y = 9, Width = 20, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel.orderNo, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "factory", X = 38, Y = 9, Width = 20, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = BuildSmallLabelFactoryOrLineNo(), FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "物料编号", X = 2, Y = 16, Width = 16, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("material"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "materialCode", X = 18, Y = 16, Width = 20, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialCode, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "Batch", X = 38, Y = 16, Width = 20, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = batchDisplay, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "物料名称", X = 2, Y = 23, Width = 16, Height = 9, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("name"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "materialName", X = 18, Y = 23, Width = 60, Height = 9, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialName, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "规格型号", X = 2, Y = 32, Width = 16, Height = 14, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("model"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "materialModel", X = 18, Y = 32, Width = 60, Height = 14, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialModel_zh, FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "数量", X = 2, Y = 46, Width = 16, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = SmallFieldCaption("qty"), FontSize = labelFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "PCS", X = 18, Y = 46, Width = 60, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = FormatSmallLabelQuantityPcsText(), FontSize = valueFs, Bold = true, Align = "left", VerticalAlign = "center" }
            },
            new()
            {
                Name = "其他信息", X = 2, Y = 54, Width = 76, Height = 4, ShowBorder = false,
                Text = new TextElement
                {
                    Content = BuildSmallLabelDateTimeWithTime(),
                    FontSize = 6f, Bold = true, Align = "left", VerticalAlign = "center"
                }
            }
        };
    }

    string BuildProductionOrderLineNo()
    {
        if (!string.IsNullOrEmpty(location_code))
            return $"{printModel!.factory}->{location_code}";

        return !string.IsNullOrEmpty(printModel!.SALES_WERKS)
            ? $"{printModel.factory}->{printModel.SALES_WERKS}"
            : printModel.factory;
    }

    (string salesOrderNo, string salesOrderLineNo) NormalizeSalesOrderFields()
    {
        var salesOrderNo = printModel!.SalesOrderNo;
        var salesOrderLineNo = printModel.SalesOrderNolineNO;

        // 对齐 WinForm：00000000 且无 ZJCVBELN 时仍显示 00000000，不清空
        if (salesOrderNo == "00000000")
        {
            if (!string.IsNullOrEmpty(printModel.ZJCVBELN))
            {
                printModel.SalesOrderNo = string.Empty;
                salesOrderNo = "*" + int.Parse(printModel.ZJCVBELN).ToString();
            }
        }

        if (salesOrderLineNo == "00")
        {
            salesOrderLineNo = "0";
            if (!string.IsNullOrEmpty(printModel.ZJCVBPOS))
                salesOrderLineNo = int.Parse(printModel.ZJCVBPOS).ToString();
        }

        var bezei8 = printModel.BEZEI8 ?? string.Empty;
        if (LanguageHelper.IsZh)
            salesOrderLineNo = salesOrderLineNo + " " + bezei8;

        return (salesOrderNo, salesOrderLineNo);
    }

    string BuildPrintRemark()
    {
        var remark = string.Empty;
        var retext = string.Empty;
        var lang = GetLangParam();

        if (if_remark)
        {
            var ordernum = Convert.ToDouble(printModel!.orderQuantity);
            var ordernumInt = Convert.ToInt32(ordernum);
            if (Math.Abs(ordernum - ordernumInt) < double.Epsilon)
                printModel.orderQuantity = ordernumInt.ToString();

            retext = lang == "1" ? "订单数量:" : "Order QTY:";
            remark = retext + printModel.orderQuantity + " " + remark;
        }

        var tabRemark = GetTabRemark();
        if (!string.IsNullOrWhiteSpace(tabRemark))
        {
            retext = lang == "1" ? "备注" : "remark";
            remark += "   " + retext + " :" + tabRemark.Trim();
        }

        if (!string.IsNullOrEmpty(_currentTray) && !string.IsNullOrEmpty(_totalTray))
        {
            retext = lang == "1" ? "第" : "Batch";
            remark += "   " + retext + ":" + _currentTray + "/" + _totalTray;
        }

        if (_isSerialOpen)
        {
            var weight = lang == "1" ? "重量：" : "weight:";
            remark += "   " + weight + WeightMeasured + "KG";
        }

        if (!string.IsNullOrEmpty(UserId))
            remark += "   工号:" + UserId;

        var daysDiff = PrinterHelper.GetDaysDifference(printModel.DGLTP);
        if (daysDiff != "")
            remark += "    Time:" + daysDiff;

        return remark;
    }

    string BuildOrderLineNo100x80(int tabIndex) => tabIndex switch
    {
        0 => BuildProductionOrderLineNo(),
        1 or 4 => printModel!.orderLineNo,
        _ => printModel!.factory
    };

    string BuildOrderLineNo100x120(int tabIndex) => tabIndex switch
    {
        0 => printModel!.factory,
        // 采购/销单库存/交货：对齐 WinForm 打 orderLineNo
        1 or 3 or 4 => printModel!.orderLineNo,
        _ => $"{printModel!.factory}->{printModel.SALES_WERKS}"
    };

    /// <summary>100×80 物料行第二格：均为批次号（WinForm grid16）；无批次时留空。</summary>
    string BuildBatchCell100x80() => GetBatchForDisplay();

    static (float x, float y, float w, float h) MasTableGrid(float xFrac, float yFrac, float wFrac, float hFrac) =>
        (1f + 96f * xFrac, 3f + 76f * yFrac, 96f * wFrac, 76f * hFrac);

    static PrintBlock MasTableBlock(
        string name,
        float xFrac,
        float yFrac,
        float wFrac,
        float hFrac,
        bool showBorder,
        string? text = null,
        float fontSize = 10f,
        bool bold = true,
        string align = "left",
        string verticalAlign = "middle",
        QrElement? qr = null)
    {
        var (x, y, w, h) = MasTableGrid(xFrac, yFrac, wFrac, hFrac);
        return new PrintBlock
        {
            Name = name,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            ShowBorder = showBorder,
            Text = string.IsNullOrEmpty(text)
                ? null
                : new TextElement
                {
                    Content = text,
                    FontName = "Arial",
                    FontSize = fontSize,
                    Bold = bold,
                    Align = align,
                    VerticalAlign = verticalAlign
                },
            Qr = qr
        };
    }

    string BuildMalaysiaMasDateTimeText()
    {
        if (GetLangParam().Contains('E', StringComparison.OrdinalIgnoreCase))
            return DateTime.Now.ToString("dd/MM/yyyy") + "\n" + DateTime.Now.ToString("HH:mm");
        return DateTime.Now.ToShortDateString() + "\n" + DateTime.Now.ToString("HH:mm");
    }

    string BuildMalaysiaMasBottomRemark()
    {
        var remark = string.Empty;
        var retext = string.Empty;
        var isZh = IsChineseLang();

        if (if_remark)
        {
            var ordernum = Convert.ToDouble(printModel!.orderQuantity);
            var ordernumInt = Convert.ToInt32(ordernum);
            if (Math.Abs(ordernum - ordernumInt) < double.Epsilon)
                printModel.orderQuantity = ordernumInt.ToString();

            retext = isZh ? "订单数量:" : "Order QTY:";
            remark = retext + printModel.orderQuantity;
        }

        if (!string.IsNullOrEmpty(_currentTray) && !string.IsNullOrEmpty(_totalTray))
        {
            retext = isZh ? "第" : "Batch";
            remark += "   " + retext + ":" + _currentTray + "/" + _totalTray;
        }

        if (_isSerialOpen)
        {
            var weight = isZh ? "重量：" : "weight:";
            remark += "   " + weight + WeightMeasured + "KG";
        }

        if (!string.IsNullOrEmpty(UserId))
            remark += "   工号：" + UserId;

        var daysDiff = PrinterHelper.GetDaysDifference(printModel!.DGLTP);
        if (daysDiff != "")
            remark += "    Time:" + daysDiff;

        return remark;
    }

    string BuildMalaysiaMasGrid14Remark()
    {
        var tabRemark = GetTabRemark()?.Trim();
        if (string.IsNullOrWhiteSpace(tabRemark))
            return string.Empty;

        var retext = IsChineseLang() ? "备注" : "remark";
        return " " + retext + " :" + tabRemark;
    }

    /// <summary>马来专属：采购不展示 ZEIVR（对齐 WinForm）；其它 Tab 物料号(批次)。</summary>
    string BuildMalaysiaMasMaterialText(int tabIndex)
    {
        if (tabIndex == 1)
            return printModel!.materialCode;

        var batchDisplay = GetBatchForDisplay();
        return string.IsNullOrEmpty(batchDisplay)
            ? printModel!.materialCode
            : $"{printModel!.materialCode}({batchDisplay})";
    }

    /// <summary>对齐 WinForm <c>is_malaysia</c> 100×80 版式。</summary>
    List<PrintBlock> BuildMalaysiaMasPrint100x80(int tabIndex)
    {
        var isZh = IsChineseLang();
        var (salesOrderNo, _) = NormalizeSalesOrderFields();
        var builder = isZh ? "马来西亚制造" : "MADE IN MALAYSIA";
        var saleNoLabel = isZh ? "销单号" : "SALE NUM.";
        var materialLabel = isZh ? "物料" : "MATERIAL";
        var materialDesLabel = isZh ? "物料描述" : "MATERIAL DES.";
        var modelLabel = isZh ? "规格型号" : "MODEL";
        var totalNumLabel = isZh ? "总数量" : "TOTAL QUANTITY";
        var qualified = if_qualify
            ? (isZh ? "合格√" : "qualified√")
            : (isZh ? "合格" : "qualified");
        var unqualified = isZh ? "不合格" : "unqualified";
        var special = isZh ? "特采" : "special";
        var datetime = BuildMalaysiaMasDateTimeText();
        var bottomRemark = BuildMalaysiaMasBottomRemark();
        var grid14Remark = BuildMalaysiaMasGrid14Remark();

        var blocks = new List<PrintBlock>
        {
            MasTableBlock("MAS grid1", 0f, 0f, 0.7f, 0.1f, true, builder, align: "center"),
            MasTableBlock("MAS grid2", 0f, 0.1f, 0.3f, 0.1f, true, saleNoLabel),
            MasTableBlock("MAS grid9", 0.3f, 0.1f, 0.4f, 0.1f, true, salesOrderNo),
            MasTableBlock("MAS grid3", 0f, 0.2f, 0.3f, 0.1f, true, materialLabel),
            MasTableBlock("MAS grid10", 0.3f, 0.2f, 0.4f, 0.1f, true, BuildMalaysiaMasMaterialText(tabIndex)),
            MasTableBlock("MAS grid4", 0f, 0.3f, 0.3f, 0.15f, true, materialDesLabel),
            MasTableBlock("MAS grid11", 0.3f, 0.3f, 0.7f, 0.15f, true, printModel!.materialName),
            MasTableBlock("MAS grid5", 0f, 0.45f, 0.3f, 0.2f, true, modelLabel),
            MasTableBlock("MAS grid12", 0.3f, 0.45f, 0.7f, 0.2f, true, printModel.materialModel_zh),
            MasTableBlock("MAS grid6", 0f, 0.65f, 0.3f, 0.15f, true, totalNumLabel),
            MasTableBlock("MAS grid13", 0.3f, 0.65f, 0.23f, 0.15f, true, printModel.amountQuantity, fontSize: 12f, align: "center"),
            MasTableBlock("MAS grid7", 0f, 0.8f, 0.3f, 0.15f, true, datetime, fontSize: 10f),
            MasTableBlock("MAS grid15", 0.3f, 0.8f, 0.23f, 0.15f, false, qualified, fontSize: 11f, align: "center"),
            MasTableBlock("MAS grid16", 0.53f, 0.8f, 0.24f, 0.15f, false, unqualified, fontSize: 11f, align: "center"),
            MasTableBlock("MAS grid17", 0.77f, 0.8f, 0.23f, 0.15f, false, special, fontSize: 11f, align: "center"),
            MasTableBlock("MAS grid19", 0.3f, 0.8f, 0.7f, 0.15f, true),
            MasTableBlock("MAS grid8", 0f, 0.95f, 1f, 0.08f, false, bottomRemark, fontSize: 8f),
            MasTableBlock("MAS grid18", 0.7f, 0f, 0.3f, 0.3f, true, qr: new QrElement
            {
                Value = QrValue ?? string.Empty,
                ErrorCorrection = "M",
                Charset = "UTF-8",
                SafeMarginMm = 0.5f
            })
        };

        blocks.Insert(11, string.IsNullOrWhiteSpace(grid14Remark)
            ? MasTableBlock("MAS grid14", 0.53f, 0.65f, 0.47f, 0.15f, true)
            : MasTableBlock("MAS grid14", 0.53f, 0.65f, 0.47f, 0.15f, true, grid14Remark, fontSize: 8f));

        return blocks;
    }

    /// <summary>对齐 WinForm <c>is_malaysia2</c> 100×80 版式。</summary>
    List<PrintBlock> BuildMalaysia2Print100x80()
    {
        var qualified = if_qualify ? "qualified√" : "qualified";
        var datetime = DateTime.Now.ToString("yyyy/MM/dd") + "\n" + DateTime.Now.ToString("HH:mm");

        return
        [
            MasTableBlock("MAS2 grid1", 0f, 0f, 0.3f, 0.1f, true, "PO#/Line#", align: "center"),
            MasTableBlock("MAS2 grid2", 0.3f, 0f, 0.3f, 0.1f, true, printModel!.orderNo, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid3", 0.6f, 0f, 0.2f, 0.1f, true, printModel.orderLineNo, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid4", 0.8f, 0f, 0.2f, 0.3f, true, qr: new QrElement
            {
                Value = QrValue ?? string.Empty,
                ErrorCorrection = "M",
                Charset = "UTF-8",
                SafeMarginMm = 0.5f
            }),
            MasTableBlock("MAS2 grid5", 0f, 0.1f, 0.3f, 0.1f, true, "SO#/Line#", align: "center"),
            MasTableBlock("MAS2 grid6", 0.3f, 0.1f, 0.3f, 0.1f, true, printModel.SalesOrderNo, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid7", 0.6f, 0.1f, 0.2f, 0.1f, true, printModel.SalesOrderNolineNO, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid8", 0f, 0.2f, 0.3f, 0.1f, true, "SAP Code", align: "center"),
            MasTableBlock("MAS2 grid9", 0.3f, 0.2f, 0.3f, 0.1f, true, printModel.materialCode, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid10", 0.6f, 0.2f, 0.2f, 0.1f, true),
            MasTableBlock("MAS2 grid11", 0f, 0.3f, 0.3f, 0.15f, true, "Description\n(CN)", align: "center"),
            MasTableBlock("MAS2 grid12", 0.3f, 0.3f, 0.7f, 0.15f, true, printModel.materialModel_zh, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid13", 0f, 0.45f, 0.3f, 0.15f, true, "Description\n(EN)", align: "center"),
            MasTableBlock("MAS2 grid14", 0.3f, 0.45f, 0.7f, 0.15f, true, printModel.materialModel_en, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid15", 0f, 0.6f, 0.3f, 0.2f, true, "Quantity/carton", align: "center"),
            MasTableBlock("MAS2 grid16", 0.3f, 0.6f, 0.23f, 0.2f, true, printModel.boxQuantity, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid17", 0.53f, 0.6f, 0.23f, 0.2f, true, "Quantity(Total)", align: "center"),
            MasTableBlock("MAS2 grid18", 0.76f, 0.6f, 0.24f, 0.2f, true, printModel.amountQuantity, fontSize: 11f, bold: false, align: "center"),
            MasTableBlock("MAS2 grid19", 0f, 0.8f, 0.3f, 0.2f, true, datetime, align: "center"),
            MasTableBlock("MAS2 grid20", 0.3f, 0.8f, 0.22f, 0.2f, false, qualified, fontSize: 11f, align: "center"),
            MasTableBlock("MAS2 grid21", 0.52f, 0.8f, 0.26f, 0.2f, false, "unqualified", fontSize: 11f, align: "center"),
            MasTableBlock("MAS2 grid22", 0.78f, 0.8f, 0.22f, 0.2f, false, "special", fontSize: 11f, align: "center"),
            MasTableBlock("MAS2 grid23", 0.3f, 0.8f, 0.7f, 0.2f, true)
        ];
    }

    List<PrintBlock> BuildStandardPrint100x80(string orderLineNo, int tabIndex, bool bold)
    {
        var (salesOrderNo, salesOrderLineNo) = NormalizeSalesOrderFields();
        var datetime = BuildLabelDateTimeText();
        // V2：唯一码画在日期格下半（对齐 WinForm DrawLabelUniqueCodeV2InQualifiedRow）
        if (UseProductInboundQrV2() && !string.IsNullOrEmpty(_currentLabelUniqueCodeV2))
            datetime = datetime + "\n" + _currentLabelUniqueCodeV2;
        var quality = if_qualify ? T["Qualified"] + "√" : T["Qualified"];
        var remark = BuildPrintRemark();
        var batchCell = BuildBatchCell100x80();
        var isPurchaseTab = tabIndex == 1;
        var labelFs = FieldCaptionFontSize(10);

        var blocks = new List<PrintBlock>
        {
            new()
            {
                Name = "Block 1", X = 2, Y = 4, Width = 25, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Supplier", "Supplier"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 2", X = 27, Y = 4, Width = 46, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = printModel!.supplier, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 3", X = 73, Y = 4, Width = 25, Height = 29, ShowBorder = true,
                Qr = new QrElement { Value = QrValue ?? "", ErrorCorrection = "M", Charset = "UTF-8", SafeMarginMm = 0.5f }
            },
            new()
            {
                Name = "Block 4", X = 2, Y = 12, Width = 25, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Order&Row", "Order&row"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 5", X = 27, Y = 12, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel.orderNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 6", X = 50, Y = 12, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = orderLineNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 7", X = 2, Y = 19, Width = 25, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("SaleNo&Row", "SaleNo.&row"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 8", X = 27, Y = 19, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = salesOrderNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 9", X = 50, Y = 19, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = salesOrderLineNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 10", X = 2, Y = 26, Width = 25, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Material", "Material"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 11", X = 27, Y = 26, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialCode, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 12", X = 50, Y = 26, Width = 23, Height = 7, ShowBorder = true,
                Text = new TextElement { Content = batchCell, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 13", X = 2, Y = 33, Width = 25, Height = 13, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("MaterialDes.", "Material Des."), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 14", X = 27, Y = 33, Width = 71, Height = 13, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialName, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 15", X = 2, Y = 46, Width = 25, Height = 13, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Model", "Model"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 16", X = 27, Y = 46, Width = 71, Height = 13, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialModel_zh, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 17", X = 2, Y = 59, Width = 25, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Quantity", "Quantity"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 18", X = 27, Y = 59, Width = 20, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = printModel.boxQuantity, FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 19", X = 47, Y = 59, Width = 33, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("TotalQuantity", "TotalQuantity"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 20", X = 80, Y = 59, Width = 18, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = printModel.amountQuantity, FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 21", X = 2, Y = 67, Width = 25, Height = 8, ShowBorder = true,
                Text = new TextElement { Content = datetime, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 22", X = 27, Y = 67, Width = 20, Height = 8, ShowBorder = false,
                Text = new TextElement { Content = quality, FontName = "Arial", FontSize = 10, Bold = bold, Align = "right", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 23", X = 47, Y = 67, Width = 33, Height = 8, ShowBorder = false,
                Text = new TextElement { Content = T["UnQualified"], FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 24", X = 80, Y = 67, Width = 18, Height = 8, ShowBorder = false,
                Text = new TextElement { Content = T["SpecialMa"], FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 25", X = 2, Y = 76, Width = 96, Height = 4, ShowBorder = false,
                Text = new TextElement { Content = remark, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 26", X = 27, Y = 67, Width = 71, Height = 8, ShowBorder = true
            }
        };

        // 采购收货 100×80：WinForm grid15 左物料号、右 ZEIVR，grid16 为批次
        if (isPurchaseTab)
        {
            blocks.Add(new PrintBlock
            {
                Name = "Block 11 ZEIVR", X = 27, Y = 26, Width = 23, Height = 7, ShowBorder = false,
                Text = new TextElement
                {
                    Content = printModel!.ZEIVR ?? string.Empty,
                    FontName = "Arial",
                    FontSize = 10,
                    Bold = bold,
                    Align = "right",
                    VerticalAlign = "middle"
                }
            });
        }

        return blocks;
    }

    List<PrintBlock> BuildStandardPrint100x120(string orderLineNo, int tabIndex, bool bold)
    {
        var (salesOrderNo, salesOrderLineNo) = NormalizeSalesOrderFields();
        var datetime = BuildLabelDateTimeText();
        if (UseProductInboundQrV2() && !string.IsNullOrEmpty(_currentLabelUniqueCodeV2))
            datetime = datetime + "\n" + _currentLabelUniqueCodeV2;
        var quality = if_qualify ? T["Qualified"] + "√" : T["Qualified"];
        var remark = BuildPrintRemark();
        var isPurchaseTab = tabIndex == 1;
        var labelFs = FieldCaptionFontSize(10);

        var blocks = new List<PrintBlock>
        {
            new()
            {
                Name = "Block 1", X = 2, Y = 4, Width = 25, Height = 15, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Supplier", "Supplier"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 2", X = 27, Y = 4, Width = 46, Height = 15, ShowBorder = true,
                Text = new TextElement { Content = printModel!.supplier, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 3", X = 73, Y = 4, Width = 25, Height = 48, ShowBorder = true,
                Qr = new QrElement { Value = QrValue ?? "", ErrorCorrection = "M", Charset = "UTF-8", SafeMarginMm = 0.5f }
            },
            new()
            {
                Name = "Block 4", X = 2, Y = 19, Width = 25, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Order&Row", "Order&row"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 5", X = 27, Y = 19, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = printModel.orderNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 6", X = 50, Y = 19, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = orderLineNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 7", X = 2, Y = 30, Width = 25, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("SaleNo&Row", "SaleNo.&row"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 8", X = 27, Y = 30, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = salesOrderNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 9", X = 50, Y = 30, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = salesOrderLineNo, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 10", X = 2, Y = 41, Width = 25, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Material", "Material"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 11", X = 27, Y = 41, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialCode, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 12", X = 50, Y = 41, Width = 23, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = GetBatchForDisplay(), FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 13", X = 2, Y = 52, Width = 25, Height = 18, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("MaterialDes.", "Material Des."), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 14", X = 27, Y = 52, Width = 71, Height = 18, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialName, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 15", X = 2, Y = 70, Width = 25, Height = 18, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Model", "Model"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 16", X = 27, Y = 70, Width = 71, Height = 18, ShowBorder = true,
                Text = new TextElement { Content = printModel.materialModel_zh, FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 17", X = 2, Y = 88, Width = 25, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("Quantity", "Quantity"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 18", X = 27, Y = 88, Width = 20, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = printModel.boxQuantity, FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 19", X = 47, Y = 88, Width = 33, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = FieldCaption("TotalQuantity", "TotalQuantity"), FontName = "Arial", FontSize = labelFs, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 20", X = 80, Y = 88, Width = 18, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = printModel.amountQuantity, FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 21", X = 2, Y = 99, Width = 25, Height = 11, ShowBorder = true,
                Text = new TextElement { Content = datetime, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 22", X = 27, Y = 99, Width = 20, Height = 11, ShowBorder = false,
                Text = new TextElement { Content = quality, FontName = "Arial", FontSize = 10, Bold = bold, Align = "right", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 23", X = 47, Y = 99, Width = 33, Height = 11, ShowBorder = false,
                Text = new TextElement { Content = T["UnQualified"], FontName = "Arial", FontSize = 10, Bold = bold, Align = "center", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 24", X = 80, Y = 99, Width = 18, Height = 11, ShowBorder = false,
                Text = new TextElement { Content = T["SpecialMa"], FontName = "Arial", FontSize = 10, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 25", X = 2, Y = 110, Width = 96, Height = 8, ShowBorder = false,
                Text = new TextElement { Content = remark, FontName = "Arial", FontSize = 9, Bold = bold, Align = "left", VerticalAlign = "middle" }
            },
            new()
            {
                Name = "Block 26", X = 27, Y = 99, Width = 71, Height = 11, ShowBorder = true
            }
        };

        // 采购收货 100×120：物料格右侧 ZEIVR（对齐 WinForm）
        if (isPurchaseTab)
        {
            blocks.Add(new PrintBlock
            {
                Name = "Block 11 ZEIVR", X = 27, Y = 41, Width = 23, Height = 11, ShowBorder = false,
                Text = new TextElement
                {
                    Content = printModel!.ZEIVR ?? string.Empty,
                    FontName = "Arial",
                    FontSize = 10,
                    Bold = bold,
                    Align = "right",
                    VerticalAlign = "middle"
                }
            });
        }

        return blocks;
    }

    List<PrintBlock> BuildTabPrint100x80(int tabIndex) =>
        BuildStandardPrint100x80(BuildOrderLineNo100x80(tabIndex), tabIndex, bold: false);

    List<PrintBlock> BuildTabPrint100x120(int tabIndex) =>
        BuildStandardPrint100x120(BuildOrderLineNo100x120(tabIndex), tabIndex, bold: true);

    void ApplyBatchNoForTab(int tabIndex)
    {
        batchNo = string.Empty;
        if (!string.IsNullOrWhiteSpace(PO_NO))
        {
            batchNo = PO_NO.Trim();
            if (batchNo.Length > 10)
                batchNo = batchNo[..10];
            return;
        }

        var model = printModel!;
        switch (tabIndex)
        {
            case 0:
                batchNo = model.batchRule switch
                {
                    "1" => "JC" + model.factory,
                    "2" => LSP ?? string.Empty,
                    "3" => model.orderNo,
                    "4" => LSP ?? string.Empty,
                    _ => batchNo
                };
                break;
            case 1:
                batchNo = model.batchRule switch
                {
                    "1" => "JC" + model.factory,
                    "2" => model.orderNo,
                    "3" => LSP ?? string.Empty,
                    "4" => LSP ?? string.Empty,
                    _ => batchNo
                };
                if (!string.IsNullOrEmpty(model.var1))
                    batchNo = model.var1;
                break;
            case 2:
                batchNo = model.batchRule switch
                {
                    "3" => LSP ?? string.Empty,
                    _ => batchNo
                };
                if (string.IsNullOrEmpty(model.batchRule))
                    batchNo = model.client ?? string.Empty;
                break;
            case 4:
                batchNo = model.batchNo ?? string.Empty;
                break;
        }

        if (model.batchRule == "4")
            batchNo = LSP ?? string.Empty;

        // 对齐 WinForm：批次最长 10 位
        if (!string.IsNullOrEmpty(batchNo) && batchNo.Length > 10)
            batchNo = batchNo[..10];
    }

    async Task<bool> GeneratePrintDataCoreAsync(int tabIndex)
    {
        if (!ValidateSpecialPrintMode())
            return false;

        if (printModel is null)
        {
            SnackbarHelper.Show("暂无可打印的数据，请先查询", Severity.Warning);
            return false;
        }

        if (string.IsNullOrEmpty(PrintCount))
        {
            SnackbarHelper.Show("打印数量为空", Severity.Error);
            return false;
        }

        if (string.IsNullOrEmpty(BoxNum))
        {
            SnackbarHelper.Show("请输入箱数", Severity.Error);
            return false;
        }

        if (string.IsNullOrEmpty(SumNum))
        {
            SnackbarHelper.Show("请输入合计", Severity.Error);
            return false;
        }

        if (Convert.ToDouble(SumNum) > Convert.ToDouble(printModel.orderQuantity))
        {
            SnackbarHelper.Show("总数量大于订单数量", Severity.Error);
            return false;
        }

        if (Convert.ToDouble(BoxNum) > Convert.ToDouble(SumNum))
        {
            SnackbarHelper.Show("每箱数量大于合计数量", Severity.Error);
            return false;
        }

        _currentTray = "1";
        _totalTray = PrintCount.Trim();
        _currentLabelUniqueCodeV2 = string.Empty;

        if (printModel.SalesOrderNo == null)
        {
            printModel.SalesOrderNo = "00000000";
            printModel.SalesOrderNolineNO = "00";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(printModel.SalesOrderNo) || printModel.SalesOrderNo == "0")
                printModel.SalesOrderNo = "00000000";

            if (string.IsNullOrWhiteSpace(printModel.SalesOrderNolineNO) || printModel.SalesOrderNolineNO == "0")
                printModel.SalesOrderNolineNO = "00";
        }

        if (printModel.orderNo == null)
        {
            printModel.orderNo = "00000000";
            printModel.orderLineNo = "00";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(printModel.orderNo) || printModel.orderNo == "0")
                printModel.orderNo = "00000000";

            if (string.IsNullOrWhiteSpace(printModel.orderLineNo) || printModel.orderLineNo == "0")
                printModel.orderLineNo = "00";
        }

        printModel.boxQuantity = BoxNum;
        printModel.amountQuantity = SumNum;
        printModel.amountQuantity_real = SumNum;

        ApplyBatchNoForTab(tabIndex);

        var hideBatch = ShouldHideBatchForPrint();
        try
        {
            if (UseProductInboundQrV2())
            {
                // V2：不调 GetHU，唯一码进二维码
                printModel.HUValue = string.Empty;
                printModel.QRcodeValue = string.Empty;
                QrValue = await BuildProductInboundQrValueV2FromModelAsync(batchNo, hideBatch);
            }
            else
            {
                printModel.HUValue = await GetHUAsync(printModel.factory, Guid.NewGuid().ToString("N"));
                printModel.QRcodeValue = printModel.orderNo + "/" + printModel.orderLineNo + "/" + printModel.materialCode + "/" +
                                         printModel.amountQuantity + "/" + printModel.amountQuantity_real + "/" +
                                         printModel.HUValue + "/" + printModel.SalesOrderNo + "/" + printModel.SalesOrderNolineNO;

                // 对齐 WinForm BuildLabelQrValue：无批次时仍带 /BATCH/ 后缀、值为空
                if (hideBatch)
                    QrValue = printModel.QRcodeValue + "/BATCH/";
                else if (!string.IsNullOrEmpty(batchNo))
                    QrValue = printModel.QRcodeValue + "/BATCH/" + batchNo;
                else
                    QrValue = printModel.QRcodeValue;
            }
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show("生成二维码失败：" + ex.Message, Severity.Error);
            AppendLog("生成二维码失败：" + ex.Message);
            return false;
        }

        IsGenerating = true;
        try
        {
            QrImage = QrCodeHelper.GenerateQrBase64(QrValue);
            AppendLog(QrValue ?? string.Empty);
        }
        finally
        {
            IsGenerating = false;
            await InvokeAsync(StateHasChanged);
        }

        return true;
    }

    List<PrintBlock> Production_Print100x80() => BuildTabPrint100x80(0);

    List<PrintBlock> Production_Print100x120() => BuildTabPrint100x120(0);

    #endregion

    #region Tab0 生产入库 — 打印准备

    Task<bool> Production_GenerateAsync() => GeneratePrintDataCoreAsync(0);

    #endregion

    #region Tab1 采购收货 — 打印 Blocks

    /// <summary>采购 60×40：行号格=orderLineNo，物料格右 ZEIVR（对齐 WinForm，不再复用生产版）。</summary>
    List<PrintBlock> Purchase_Print60x40()
    {
        var blocks = Production_Print60x40();
        foreach (var b in blocks)
        {
            if (b.Name == "Block 6" && b.Text is not null)
                b.Text.Content = printModel!.orderLineNo;
            if (b.Name == "Block 15" && b.Text is not null)
                b.Text.Content = FormatSmallLabelQuantityPcsText();
        }

        blocks.Add(new PrintBlock
        {
            Name = "Block 8 ZEIVR", X = 14, Y = 13, Width = 14, Height = 5, ShowBorder = false,
            Text = new TextElement
            {
                Content = printModel!.ZEIVR ?? string.Empty,
                FontName = "Arial",
                FontSize = 6,
                Bold = false,
                Align = "right",
                VerticalAlign = "middle"
            }
        });
        return blocks;
    }

    /// <summary>采购 80×60：行号格=orderLineNo，物料格右 ZEIVR。</summary>
    List<PrintBlock> Purchase_Print80x60()
    {
        var blocks = Production_Print80x60();
        foreach (var b in blocks)
        {
            if (b.Name == "factory" && b.Text is not null)
                b.Text.Content = printModel!.orderLineNo;
            if (b.Name == "PCS" && b.Text is not null)
                b.Text.Content = FormatSmallLabelQuantityPcsText();
        }

        blocks.Add(new PrintBlock
        {
            Name = "materialCode ZEIVR", X = 18, Y = 16, Width = 20, Height = 7, ShowBorder = false,
            Text = new TextElement
            {
                Content = printModel!.ZEIVR ?? string.Empty,
                FontSize = 8f,
                Bold = true,
                Align = "right",
                VerticalAlign = "center"
            }
        });
        return blocks;
    }

    List<PrintBlock> Purchase_Print100x80() => BuildTabPrint100x80(1);
    List<PrintBlock> Purchase_Print100x120() => BuildTabPrint100x120(1);

    #endregion

    #region Tab1 采购收货 — 打印准备

    Task<bool> Purchase_GenerateAsync() => GeneratePrintDataCoreAsync(1);

    #endregion

    #region Tab2 物料打印 — 打印 Blocks

    List<PrintBlock> Material_Print60x40() => Production_Print60x40();
    List<PrintBlock> Material_Print80x60() => Production_Print80x60();
    List<PrintBlock> Material_Print100x80() => BuildTabPrint100x80(2);
    List<PrintBlock> Material_Print100x120() => BuildTabPrint100x120(2);

    #endregion

    #region Tab2 物料打印 — 打印准备

    Task<bool> Material_GenerateAsync() => GeneratePrintDataCoreAsync(2);

    #endregion

    #region Tab3 销单库存 — 打印 Blocks

    List<PrintBlock> SalesStock_Print60x40() => Production_Print60x40();
    List<PrintBlock> SalesStock_Print80x60() => Production_Print80x60();
    List<PrintBlock> SalesStock_Print100x80() => BuildTabPrint100x80(3);
    List<PrintBlock> SalesStock_Print100x120() => BuildTabPrint100x120(3);

    #endregion

    #region Tab3 销单库存 — 打印准备

    Task<bool> SalesStock_GenerateAsync() => GeneratePrintDataCoreAsync(3);

    #endregion

    #region Tab4 交货订单 — 打印 Blocks

    List<PrintBlock> Delivery_Print60x40() => Production_Print60x40();
    List<PrintBlock> Delivery_Print80x60() => Production_Print80x60();
    List<PrintBlock> Delivery_Print100x80() => BuildTabPrint100x80(4);
    List<PrintBlock> Delivery_Print100x120() => BuildTabPrint100x120(4);

    #endregion

    #region Tab4 交货订单 — 打印准备

    Task<bool> Delivery_GenerateAsync() => GeneratePrintDataCoreAsync(4);

    #endregion

    #region 称重 — WebSerial

    /// <summary>配置默认马来模式：仅当特殊打印仍为「无」时套用 appsettings。</summary>
    void ApplyMalaysiaConfigDefault()
    {
        if (!string.Equals(SpecialPrint, "无", StringComparison.Ordinal))
            return;

        if (string.Equals(Config["is_malaysia2"], "true", StringComparison.OrdinalIgnoreCase))
            SpecialPrint = "电子料纸箱";
        else if (string.Equals(Config["is_malaysia"], "true", StringComparison.OrdinalIgnoreCase))
            SpecialPrint = "马来专属";

        if ((PrintIsMalaysia || PrintIsMalaysia2) && PaperSize is not "100*80")
            PaperSize = "100*80";
    }

    /// <summary>对齐 WinForm purchaseFunction：控制可见 Tab。</summary>
    void ApplyPurchaseFunctionConfig()
    {
        var code = Config["purchaseFunction"]?.Trim() ?? string.Empty;
        switch (code)
        {
            case "4c58a367cec14362a0bee1da5dedd57a":
                // 仅生产
                VisibleLogicalTabs = [0];
                break;
            case "26c95b3ddb2347f0ab9bd8735a7554be":
            case "":
                // 全部（空配置时 Web 默认全开，避免误藏）
                VisibleLogicalTabs = [0, 1, 2, 3, 4];
                break;
            default:
                // 仅采购（与 WinForm default 分支一致），并关闭备注数量
                VisibleLogicalTabs = [1];
                if_remark = false;
                break;
        }

        if (activeTabIndex >= VisibleLogicalTabs.Count)
            activeTabIndex = 0;
    }

    /// <summary>对齐 WinForm autoRemarkOrder：false 时默认不勾选备注数量。</summary>
    void ApplyAutoRemarkOrderConfig()
    {
        if (string.Equals(Config["autoRemarkOrder"], "false", StringComparison.OrdinalIgnoreCase))
            if_remark = false;
    }

    async Task OpenBomDialogAsync()
    {
        var orderNo = ProdOrderNo?.Trim();
        if (string.IsNullOrEmpty(orderNo) && printModel is not null)
            orderNo = printModel.orderNo;

        if (string.IsNullOrWhiteSpace(orderNo))
        {
            SnackbarHelper.Show("请先输入生产订单号", Severity.Warning);
            return;
        }

        var parameters = new DialogParameters
        {
            [nameof(ProductionBomDialog.ProductionOrderNo)] = orderNo
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        await DialogService.ShowAsync<ProductionBomDialog>("生产订单 BOM", parameters, options);
    }

    async Task OpenReportDialogAsync()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        await DialogService.ShowAsync<ProductionReportDialog>("报工", options);
    }

    async Task OnSpecialPrintChangedAsync()
    {
        if (PrintIsMalaysia || PrintIsMalaysia2)
            PaperSize = "100*80";

        await SavePrintSettingsAsync();
    }

    async Task OnPaperSizeChangedAsync()
    {
        // 马来专属/电子料纸箱仅支持 100×80；改其它纸张时退回「无」
        if (PaperSize is not "100*80" && (PrintIsMalaysia || PrintIsMalaysia2))
            SpecialPrint = "无";

        await SavePrintSettingsAsync();
    }

    async Task LoadWeightSettingsAsync()
    {
        try
        {
            WeightStandard = await JS.InvokeAsync<string?>("localStorage.getItem", WeightStandardStorageKey) ?? string.Empty;
            WeightTolerance = await JS.InvokeAsync<string?>("localStorage.getItem", WeightToleranceStorageKey) ?? string.Empty;
        }
        catch
        {
            // ignore
        }
    }

    async Task SaveWeightSettingsAsync()
    {
        if (!CanUseWeightControls) return;

        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", WeightStandardStorageKey, WeightStandard ?? string.Empty);
            await JS.InvokeVoidAsync("localStorage.setItem", WeightToleranceStorageKey, WeightTolerance ?? string.Empty);
        }
        catch
        {
            // ignore
        }
    }

    async Task EnableWeight()
    {
        if (!_isSerialOpen && !CanUseWeightControls)
        {
            SnackbarHelper.Show($"工号必须为 {ValidWeightUserId} 才能连接称重器", Severity.Warning);
            return;
        }

        try
        {
            if (_isSerialOpen)
            {
                await JS.InvokeVoidAsync("serialInterop.closePort");
                _isSerialOpen = false;
                WeightMeasured = string.Empty;
                SnackbarHelper.Show("称重器已断开", Severity.Info);
                AppendLog("WebSerial 称重连接已关闭");
                await InvokeAsync(StateHasChanged);
                return;
            }

            var baud = await JS.InvokeAsync<string>("serialInterop.getPreferredScaleBaud");
            var br = int.TryParse(baud, out var n) ? n : 19200;
            var config = new
            {
                baudRate = baud,
                parity = "none",
                dataBits = "8",
                stopBits = "1",
                autoScaleBaud = br == 9600 || br == 19200
            };

            var success = await JS.InvokeAsync<bool>("serialInterop.openPort", config, _objRef);
            if (!success)
            {
                SnackbarHelper.Show("称重器连接失败或已取消", Severity.Error);
                AppendLog("WebSerial 称重连接失败或已取消");
                return;
            }

            _isSerialOpen = true;
            SnackbarHelper.Show("称重器连接成功", Severity.Success);
            AppendLog("WebSerial 称重连接成功");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            SnackbarHelper.Show($"称重器连接异常: {ex.Message}", Severity.Error);
            AppendLog($"称重器连接异常: {ex.Message}");
        }
    }

    [JSInvokable]
    public async Task ReceiveData(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        WeightMeasured = data.Trim();
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnError(string errorMessage)
    {
        _isSerialOpen = false;
        AppendLog($"串口错误: {errorMessage}");
        await InvokeAsync(StateHasChanged);
    }

    static bool TryParseDecimal(string? text, out decimal value)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    bool IsWeightOutOfTolerance()
    {
        if (!_isSerialOpen) return false;
        if (!TryParseDecimal(WeightStandard, out var standard)) return false;
        if (!TryParseDecimal(WeightMeasured, out var actual)) return false;
        if (!TryParseDecimal(WeightTolerance, out var tolerance)) return false;
        if (tolerance < 0) tolerance = Math.Abs(tolerance);
        return Math.Abs(actual - standard) > tolerance;
    }

    bool ValidateWeightBeforePrint()
    {
        if (!_isSerialOpen) return true;

        if (!TryParseDecimal(WeightMeasured, out _))
        {
            SnackbarHelper.Show(LanguageHelper.IsZh ? "检查重量格式！" : "Check the weight format!", Severity.Error);
            AppendLog("打印拦截：称重数据格式无效");
            return false;
        }

        if (!TryParseDecimal(WeightStandard, out _) || !TryParseDecimal(WeightTolerance, out _))
        {
            SnackbarHelper.Show(LanguageHelper.IsZh ? "请先设定标重和公差" : "Please set standard weight and tolerance", Severity.Warning);
            AppendLog("打印拦截：标重或公差未设定");
            return false;
        }

        if (IsWeightOutOfTolerance())
        {
            SnackbarHelper.Show(LanguageHelper.IsZh ? "公差大于设定值，请检查！" : "The tolerance exceeds the set value, please check!", Severity.Error);
            AppendLog("打印拦截：实际称重超出公差范围");
            return false;
        }

        return true;
    }

    #endregion

    #region 公共 — 生命周期与布局

    async Task LoadPrintSettingsAsync()
    {
        MaterialLabelPrintSettings? settings = null;

        try
        {
            var json = await JS.InvokeAsync<string?>("localStorage.getItem", PrintSettingsStorageKey);
            if (!string.IsNullOrWhiteSpace(json))
                settings = JsonConvert.DeserializeObject<MaterialLabelPrintSettings>(json);
        }
        catch
        {
            // 忽略损坏的缓存
        }

        if (settings != null)
        {
            ApplyPrintSettings(settings);
            return;
        }

        try
        {
            var lastUsed = await PrinterHelper.GetLastUsedPrinterAsync(JS, LegacyPrinterStorageKey);
            if (!string.IsNullOrEmpty(lastUsed) && Printers.Contains(lastUsed))
                SelectedPrinter = lastUsed;
        }
        catch
        {
            // 忽略
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            SelectedPrinter = !string.IsNullOrWhiteSpace(DefaultPrinter) && Printers.Contains(DefaultPrinter)
                ? DefaultPrinter
                : Printers.FirstOrDefault();
        }
    }

    void ApplyPrintSettings(MaterialLabelPrintSettings settings)
    {
        // 打印机列表可能尚未拉取：先记下偏好，列表回来后再校验
        if (!string.IsNullOrWhiteSpace(settings.SelectedPrinter))
            SelectedPrinter = settings.SelectedPrinter;

        if (IsValidPaperSize(settings.PaperSize))
            PaperSize = settings.PaperSize;

        if (IsValidSpecialPrint(settings.SpecialPrint))
            SpecialPrint = settings.SpecialPrint;

        if_rotate = settings.if_rotate;
        if_qualify = settings.if_qualify;
        if_remark = settings.if_remark;
        HideBatch = settings.HideBatch;
    }

    void ApplyPrinterLoadResult(PrinterLoadResult result)
    {
        if (!result.Success)
            return;

        Printers = result.Printers.ToList();
        DefaultPrinter = result.DefaultPrinter;

        // 优先沿用本页已保存的默认打印机；不在列表中则用代理解析结果
        if (!string.IsNullOrWhiteSpace(SelectedPrinter) && Printers.Contains(SelectedPrinter))
            return;

        SelectedPrinter = result.SelectedPrinter
                         ?? (!string.IsNullOrWhiteSpace(DefaultPrinter) && Printers.Contains(DefaultPrinter)
                             ? DefaultPrinter
                             : Printers.FirstOrDefault());
    }

    static bool IsValidPaperSize(string? value) =>
        value is "60*40" or "80*60" or "100*80" or "100*120" or "297*210";

    static bool IsValidSpecialPrint(string? value) =>
        value is "无" or "双语打印" or "马来专属" or "电子料纸箱";

    async Task SavePrintSettingsAsync()
    {
        try
        {
            var settings = new MaterialLabelPrintSettings
            {
                SelectedPrinter = SelectedPrinter,
                PaperSize = PaperSize,
                SpecialPrint = SpecialPrint,
                if_rotate = if_rotate,
                if_qualify = if_qualify,
                if_remark = if_remark,
                HideBatch = HideBatch
            };

            await JS.InvokeVoidAsync(
                "localStorage.setItem",
                PrintSettingsStorageKey,
                JsonConvert.SerializeObject(settings));

            await PrinterHelper.SaveLastUsedPrinterAsync(JS, SelectedPrinter, LegacyPrinterStorageKey);
        }
        catch
        {
            // 忽略 storage 写入失败
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await PrinterBootstrap.TryLoadOnFirstRenderAsync(
            firstRender,
            LegacyPrinterStorageKey,
            ApplyPrinterLoadResult,
            () => InvokeAsync(StateHasChanged));

        if (firstRender)
        {
            var resizeOptions = new ResizeOptions { NotifyOnBreakpointOnly = true };
            await BrowserViewportService.SubscribeAsync(_subscriptionId, OnBreakpointChanged, resizeOptions);

            var currentBreakpoint = await BrowserViewportService.GetCurrentBreakpointAsync();

            if (currentBreakpoint != Breakpoint.None)
            {
                UpdateLayoutStatus(currentBreakpoint);
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task OnBreakpointChanged(BrowserViewportEventArgs e)
    {
        UpdateLayoutStatus(e.Breakpoint);
        await InvokeAsync(StateHasChanged);
    }

    private void UpdateLayoutStatus(Breakpoint breakpoint)
    {
        if (breakpoint == Breakpoint.Xs || breakpoint == Breakpoint.Sm || breakpoint == Breakpoint.Md)
        {
            DynamicSpacing = 1;
        }
        else
        {
            DynamicSpacing = 2;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isSerialOpen)
        {
            try
            {
                await JS.InvokeVoidAsync("serialInterop.closePort");
            }
            catch
            {
                // ignore
            }
        }

        _objRef?.Dispose();

        if (_subscriptionId != Guid.Empty)
            await BrowserViewportService.UnsubscribeAsync(_subscriptionId);
    }

    #endregion
}
