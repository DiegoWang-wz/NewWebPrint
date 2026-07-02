namespace NewWebPrint.Models;

public class EWMLabelModel
{
    public string supplier { get; set; }

    public string factory { get; set; }

    public string orderNo { get; set; }

    public string SalesOrderNo { get; set; }

    public string orderLineNo { get; set; }

    public string materialCode { get; set; }

    public string materialGroup { get; set; }

    public string materialName { get; set; }

    public string materialModel_zh { get; set; }

    public string materialModel_en { get; set; }

    public string boxQuantity { get; set; }

    public string amountQuantity { get; set; }

    public string amountQuantity_real { get; set; }

    public string orderQuantity { get; set; }

    public string QRcodeValue { get; set; }

    public string HUValue { get; set; }

    public string OrderDelivery { get; set; }

    public string OperateDate { get; set; }

    public string OperateTime { get; set; }

    public string SalesOrderNolineNO { get; set; }

    public string client { get; set; }

    public string clientno { get; set; }

    public string jclogoType { get; set; }

    public string jclogoText { get; set; }

    public string goodsmark { get; set; }

    public string unitManhour { get; set; }

    public string amountManhour { get; set; }

    public string createDate { get; set; }

    public string rkdate { get; set; }

    public string rknum { get; set; }

    public string reportNum { get; set; }

    public string Real_deliveryDate { get; set; }

    public string batchNo { get; set; }

    public string ZNLPN { get; set; }

    public string ZCPON { get; set; }

    public string BSTKD { get; set; }

    public string STTXT { get; set; }

    public string ZOGEN { get; set; }

    public string batchRule { get; set; }

    public string AUART { get; set; }

    public string LGORT { get; set; }

    public string ZJCVBELN { get; set; }

    public string ZJCVBPOS { get; set; }

    public string BEZEI8 { get; set; }

    public string ZI10 { get; set; }

    public string ZI01 { get; set; }

    public string ZH02 { get; set; }

    public string ZI02 { get; set; }

    public string ZEINR { get; set; }

    public string ZEIVR { get; set; }

    public string var1 { get; set; }

    public string var2 { get; set; }

    public string var3 { get; set; }

    public string var4 { get; set; }

    public string var5 { get; set; }

    public string var6 { get; set; }

    public string var7 { get; set; }

    public string PurchaseType { get; set; }

    public string SpecialProcurementCategory { get; set; }

    public string DGLTP { get; set; }

    public string DLDATE { get; set; }
    public string SALES_WERKS { get; set; }

    public EWMLabelModel()
    {
        OperateDate = DateTime.Now.ToString("yyyy-MM-dd");
        OperateTime = DateTime.Now.ToString("HH:mm:ss");
    }
}
