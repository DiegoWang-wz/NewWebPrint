using Microsoft.EntityFrameworkCore;

namespace NewWebPrint.DbContexts;

/// <summary>
/// Oracle 数据库上下文。ZHIYOU 相关实体已移除，后续可在此重新添加 DbSet 与 OnModelCreating 映射。
/// </summary>
public class OracleDbContext : DbContext
{
    public OracleDbContext(DbContextOptions<OracleDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
