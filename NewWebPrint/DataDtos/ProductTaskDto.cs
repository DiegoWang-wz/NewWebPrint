namespace NewWebPrint.DataDtos;

public class ProductTaskDto
{
    public string task_id { get; set; }
    public string? title { get; set; }
    public string? description { get; set; }
    public byte status { get; set; } = 0;
    public string? assignee_id { get; set; }
    public DateTime created_at { get; set; } = DateTime.Now;
    public DateTime updated_at { get; set; } = DateTime.Now;
    public bool process_1 { get; set; }
    public bool process_2 { get; set; }
    public bool process_3 { get; set; }
    public bool process_4 { get; set; }
    public bool process_5 { get; set; }
    public bool process_6 { get; set; }
    public bool process_7 { get; set; }
    public bool process_8 { get; set; }
    
    public int product_num { get; set; } = 1;
    public string? sale_order_number { get; set; }

}