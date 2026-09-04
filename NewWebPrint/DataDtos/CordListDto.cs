namespace NewWebPrint.DataDtos;

/// <summary>
/// CORD 列表增删改 DTO（用于对话框表单）
/// </summary>
public class CordListDto
{
    public int Id { get; set; }
    public string? JCPartNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? LDPartNumber { get; set; }
    public string? LDPartName { get; set; }
    /// <summary>工厂类型：线性 / 进出口</summary>
    public string? FactoryType { get; set; }
    public string? RevisionNumber { get; set; }
    public float? PackageWeight { get; set; }
    public int? Quantity { get; set; }
}
