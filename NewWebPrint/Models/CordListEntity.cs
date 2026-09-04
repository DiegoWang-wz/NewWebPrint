using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewWebPrint.Models;

/// <summary>
/// CORD 侧唛标签物料列表表 (CORD_LIST)
/// </summary>
[Table("CORD_LIST")]
public class CordListEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(128)]
    public string? JCPartNumber { get; set; }

    [MaxLength(256)]
    public string? SupplierName { get; set; }

    [MaxLength(128)]
    public string? LDPartNumber { get; set; }

    [MaxLength(256)]
    public string? LDPartName { get; set; }

    /// <summary>工厂类型：线性 / 进出口</summary>
    [MaxLength(32)]
    public string? FactoryType { get; set; }

    [MaxLength(32)]
    public string? RevisionNumber { get; set; }

    public float? PackageWeight { get; set; }

    public int? Quantity { get; set; }
}
