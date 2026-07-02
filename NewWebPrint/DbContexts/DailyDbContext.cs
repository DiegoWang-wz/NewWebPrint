using NewWebPrint.Models;
using Microsoft.EntityFrameworkCore;

namespace NewWebPrint.DbContexts;

public class DailyDbContext : DbContext
{
    public DailyDbContext(DbContextOptions<DailyDbContext> options) : base(options)
    {
    }

    public virtual DbSet<ProductTaskModel> ProductTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
