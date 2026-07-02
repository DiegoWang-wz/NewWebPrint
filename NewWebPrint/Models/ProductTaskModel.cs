using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewWebPrint.Models;

/// <summary>
/// 生产任务主表（存储生产任务核心信息）
/// </summary>
[Table("Product_Tasks")]
public class ProductTaskModel
{
    /// <summary>
    /// 主键，自增ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }

    /// <summary>
    /// 业务任务ID（支持Unicode，如含特殊前缀）
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string task_id { get; set; }

    /// <summary>
    /// 任务标题（支持中文，如2024Q3微型舵机生产任务）
    /// </summary>
    [MaxLength(255)]
    public string? title { get; set; }

    /// <summary>
    /// 任务详细描述（大文本，支持长内容）
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 状态：0=未开始，1=进行中，2=已完成，3=已取消
    /// </summary>
    [Required]
    public byte status { get; set; } = 0;


    /// <summary>
    /// 负责人ID
    /// </summary>
    [MaxLength(64)]
    public string? assignee_id { get; set; }

    /// <summary>
    /// 创建时间（含毫秒，精确到时间戳）
    /// </summary>
    [Required]
    public DateTime created_at { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间（需通过触发器/应用层自动更新）
    /// </summary>
    [Required]
    public DateTime updated_at { get; set; } = DateTime.Now;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_1 { get; set; } = 0;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_2 { get; set; } = 0;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_3 { get; set; } = 0;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_4 { get; set; } = 0;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_5 { get; set; } = 0;

    /// <summary>
    /// 步骤：0未完成，1已完成
    /// </summary>
    [Required]
    public byte process_6 { get; set; } = 0;
    [Required] public byte process_7 { get; set; } = 0;
    [Required]
    public byte process_8 { get; set; } = 0;
    [Required]
    public int product_num { get; set; } = 1;

    [MaxLength(64)]
    public string? sale_order_number { get; set; }

}
