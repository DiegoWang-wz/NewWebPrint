namespace NewWebPrint.Utilities;

/// <summary>
/// 本地存储 / 会话存储的键名常量，与 JS（如 localAgent.js）共用时需保持一致。
/// </summary>
public static class StorageKeys
{
    /// <summary>打印代理 baseUrl，不设则 JS 端默认 localhost:5005。</summary>
    public const string PrintAgentBaseUrl = "printAgentBaseUrl";

    // ----- LabelPrintForIndustry（铭牌工业标签；不含 Serial No）-----

    public const string IndustryPlateType = "labelIndustry.plateType";
    public const string IndustryPlateItemNo = "labelIndustry.plateItemNo";
    public const string IndustryPlatePowerRate = "labelIndustry.platePowerRate";
    public const string IndustryPlateRatedLoad = "labelIndustry.plateRatedLoad";
    public const string IndustryPlateDutyCycle = "labelIndustry.plateDutyCycle";
    public const string IndustrySelectedPrinter = "labelIndustry.selectedPrinter";
    public const string IndustryLabelKind = "labelIndustry.labelKind";
    /// <summary>FA17 标签：0 条形码 55*40，1 二维码 55*40。</summary>
    public const string IndustryFa17LabelKind = "labelIndustry.fa17LabelKind";
    public const string IndustryFa17FullBoxCount = "labelIndustry.fa17FullBoxCount";
    public const string IndustryFa17OuterBoxBarcode = "labelIndustry.fa17OuterBoxBarcode";
    public const string IndustryWaLwFullBoxCount = "labelIndustry.waLwFullBoxCount";
    public const string IndustryWaLwOuterBoxBarcode = "labelIndustry.waLwOuterBoxBarcode";
    public const string IndustryFa42FullBoxCount = "labelIndustry.fa42FullBoxCount";
    public const string IndustryFa42OuterBoxBarcode = "labelIndustry.fa42OuterBoxBarcode";
    /// <summary>FA42A：0 FA42A-V，1 FA42A-H。</summary>
    public const string IndustryFa42Variant = "labelIndustry.fa42Variant";
    public const string IndustryInternalBarcodePrefix = "labelIndustry.internalBarcodePrefix";
    public const string IndustryInternalBarcodeRollSeq = "labelIndustry.internalBarcodeRollSeq";
    public const string IndustryMainTab = "labelIndustry.mainTab";

    // W*A-LW 铭牌（40*25）。字段按型号分套持久化：labelIndustry.lwV0.*（W1A）、lwV1.*（W8A）。
    public const string IndustryLwVariant = "labelIndustry.lwVariant";
    /// <summary>旧版单套字段；迁移后仍可读，新写入使用 lwV0/lwV1。</summary>
    public const string IndustryLwName = "labelIndustry.lwName";
    public const string IndustryLwDrawing = "labelIndustry.lwDrawing";
    public const string IndustryLwSupplierCode = "labelIndustry.lwSupplierCode";
    public const string IndustryLwCustomerPrefix = "labelIndustry.lwCustomerPrefix";
    public const string IndustryLwSupplierProduct = "labelIndustry.lwSupplierProduct";
    public const string IndustryLwBatch = "labelIndustry.lwBatch";
    public const string IndustryLwShowCertIcons = "labelIndustry.lwShowCertIcons";

    // ----- LabelPrintForHSK（HSK 立柱双排标签）-----

    /// <summary>纸张规格：37*10 或 44*16。</summary>
    public const string LabelPrintForHskPaperSpec = "labelPrintForHsk.paperSpec";

    // ----- LabelPrintForNosaleNO（样品侧唛）-----
    public const string NosaleWeightStandard = "labelNosale.bz";
    public const string NosaleWeightTolerance = "labelNosale.gc";

    // ----- LabelPrintForJY（居优 AM 床架标签）-----
    /// <summary>AM-床架右标签起始流水（奇数）。</summary>
    public const string LabelPrintForJyStartSeqRight = "labelPrintForJy.startSeqRight";
    /// <summary>AM-床架左标签起始流水（偶数）。</summary>
    public const string LabelPrintForJyStartSeqLeft = "labelPrintForJy.startSeqLeft";
    public const string LabelPrintForJyPoContent = "labelPrintForJy.poContent";
    public const string LabelPrintForJyPoFontSize = "labelPrintForJy.poFontSize";
    public const string LabelPrintForJyPoBold = "labelPrintForJy.poBold";
}
