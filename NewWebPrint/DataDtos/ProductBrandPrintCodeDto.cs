namespace NewWebPrint.DataDtos;

/// <summary>
/// JCControlCode.dbo.tb_ProductBrandPrint_code 查询行
/// </summary>
public class ProductBrandPrintCodeRow
{
    public long ID { get; set; }
    public string? Model { get; set; }
    public string? Maxload { get; set; }
    public string? IPX6 { get; set; }
    public string? PartNo { get; set; }
    public string? SAPNo { get; set; }
    public string? ProdDate { get; set; }
    public string? SerialNo { get; set; }
    public string? RevNo { get; set; }
    public string? Input { get; set; }
    public string? SerialNumber { get; set; }
    public string? ProductionNo { get; set; }
    /// <summary>销售订单号（新字段，老数据可为空）。</summary>
    public string? SalesOrderNo { get; set; }
    public DateTime? Createtime { get; set; }
    /// <summary>是否允许补打，默认 false。</summary>
    public bool AllowReprint { get; set; }
    /// <summary>已补打次数，默认 0。</summary>
    public int ReprintCount { get; set; }
}
