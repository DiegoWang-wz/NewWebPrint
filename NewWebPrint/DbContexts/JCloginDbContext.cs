using NewWebPrint.Models;
using Microsoft.EntityFrameworkCore;

namespace NewWebPrint.DbContexts;

public class JCloginDbContext : DbContext
{
    public JCloginDbContext(DbContextOptions<JCloginDbContext> options) : base(options)
    {
    }

    public virtual DbSet<ProductTaskModel> ProductTasks { get; set; }
    public virtual DbSet<CordListEntity> CordLists { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CORD_LIST 列长度与类型（避免与现网 nvarchar(1) 冲突，需配合执行 Scripts/AlterCordListTable.sql）
        modelBuilder.Entity<CordListEntity>(e =>
        {
            e.Property(x => x.JCPartNumber).HasMaxLength(128);
            e.Property(x => x.SupplierName).HasMaxLength(256);
            e.Property(x => x.LDPartNumber).HasMaxLength(128);
            e.Property(x => x.LDPartName).HasMaxLength(256);
            e.Property(x => x.FactoryType).HasMaxLength(32);
            e.Property(x => x.RevisionNumber).HasMaxLength(32);
        });
    }
}
