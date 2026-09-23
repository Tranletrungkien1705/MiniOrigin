using Microsoft.EntityFrameworkCore;
using MiniOrigin.Models;

namespace MiniOrigin.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _orgId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options) => _orgId = tenant.OrgId;

    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Gln> Glns => Set<Gln>();
    public DbSet<Cte> Ctes => Set<Cte>();
    public DbSet<KdeDef> Kdes => Set<KdeDef>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<LotLink> LotLinks => Set<LotLink>();
    public DbSet<TraceEvent> Events => Set<TraceEvent>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<VerifyBatch> VerifyBatches => Set<VerifyBatch>();
    public DbSet<VerifyBatchItem> VerifyBatchItems => Set<VerifyBatchItem>();
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<Can> Cans => Set<Can>();
    public DbSet<BoxItem> BoxItems => Set<BoxItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.IsNpgsql()) b.HasDefaultSchema("miniorigin");
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Gln>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Cte>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<KdeDef>(e =>
        {
            e.HasOne(x => x.Cte).WithMany(x => x.Kdes).HasForeignKey(x => x.CteId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Brand>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Lot>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();            // GLOBAL — tra cứu công khai xuyên tenant
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.OriginGln).WithMany().HasForeignKey(x => x.OriginGlnId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<LotLink>(e =>
        {
            e.HasIndex(x => new { x.ChildLotId, x.ParentLotId }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TraceEvent>(e =>
        {
            e.HasIndex(x => x.LotId);
            e.HasOne(x => x.Lot).WithMany(x => x.Events).HasForeignKey(x => x.LotId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ProductUnit>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.SerialNo }).IsUnique();   // serial duy nhất theo tenant
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<VerifyBatch>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();       // mã lần xuất ghép duy nhất theo tenant
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<VerifyBatchItem>(e =>
        {
            e.HasIndex(x => new { x.BatchId, x.IdNo }).IsUnique();     // 1 tem chỉ xuất hiện 1 lần trong 1 lần ghép
            e.HasOne(x => x.Batch).WithMany(x => x.Items).HasForeignKey(x => x.BatchId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Box>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();       // mã hộp duy nhất theo tenant
            e.HasOne(x => x.Can).WithMany(x => x.Boxes).HasForeignKey(x => x.CanId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Can>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();       // mã thùng duy nhất theo tenant
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<BoxItem>(e =>
        {
            e.HasIndex(x => new { x.BoxId, x.SerialNo }).IsUnique();   // 1 serial chỉ nằm 1 lần trong 1 hộp
            e.HasOne(x => x.Box).WithMany(x => x.Items).HasForeignKey(x => x.BoxId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
    }

    public override int SaveChanges() { StampOrg(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) { StampOrg(); return base.SaveChangesAsync(ct); }
    private void StampOrg()
    {
        foreach (var e in ChangeTracker.Entries<IOrgOwned>())
            if (e.State == EntityState.Added && e.Entity.OrgId == Guid.Empty) e.Entity.OrgId = _orgId;
    }
}
