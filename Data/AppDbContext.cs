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
    public DbSet<WarrantyType> WarrantyTypes => Set<WarrantyType>();
    public DbSet<MaterialType> MaterialTypes => Set<MaterialType>();
    public DbSet<PartUnit> PartUnits => Set<PartUnit>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductColor> ProductColors => Set<ProductColor>();
    public DbSet<ProductColorMap> ProductColorMaps => Set<ProductColorMap>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<LotLink> LotLinks => Set<LotLink>();
    public DbSet<TraceEvent> Events => Set<TraceEvent>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<VerifyBatch> VerifyBatches => Set<VerifyBatch>();
    public DbSet<VerifyBatchItem> VerifyBatchItems => Set<VerifyBatchItem>();
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<Can> Cans => Set<Can>();
    public DbSet<BoxItem> BoxItems => Set<BoxItem>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<BomType> BomTypes => Set<BomType>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<DealerType> DealerTypes => Set<DealerType>();
    public DbSet<Dealer> Dealers => Set<Dealer>();
    public DbSet<TraceTemplate> TraceTemplates => Set<TraceTemplate>();
    public DbSet<TraceTemplateCte> TraceTemplateCtes => Set<TraceTemplateCte>();
    public DbSet<TraceTemplateKde> TraceTemplateKdes => Set<TraceTemplateKde>();
    public DbSet<TraceTemplateCteKde> TraceTemplateCteKdes => Set<TraceTemplateCteKde>();

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
        b.Entity<WarrantyType>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<MaterialType>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<PartUnit>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Supplier>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId);
            e.HasOne(x => x.WarrantyType).WithMany().HasForeignKey(x => x.WarrantyTypeId);
            e.HasOne(x => x.MaterialType).WithMany().HasForeignKey(x => x.MaterialTypeId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ProductColor>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<ProductColorMap>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.ProductId, x.ColorId }).IsUnique();   // 1 cặp sản phẩm-màu chỉ gán 1 lần
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Color).WithMany().HasForeignKey(x => x.ColorId);
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
        b.Entity<SearchLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.SearchCode });            // tra cứu nhanh theo mã
            e.HasIndex(x => x.SearchDTime);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<BomType>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Bom>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();       // mã BOM duy nhất theo tenant
            e.HasOne(x => x.ParentProduct).WithMany().HasForeignKey(x => x.ParentProductId);
            e.HasOne(x => x.BomType).WithMany().HasForeignKey(x => x.BomTypeId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<BomLine>(e =>
        {
            e.HasIndex(x => new { x.BomId, x.ComponentProductId }).IsUnique();   // 1 thành phần chỉ xuất hiện 1 lần trong 1 BOM
            e.Property(x => x.Qty).HasPrecision(18, 3);
            e.Property(x => x.ValCost).HasPrecision(18, 3);
            e.HasOne(x => x.Bom).WithMany(x => x.Lines).HasForeignKey(x => x.BomId);
            e.HasOne(x => x.ComponentProduct).WithMany().HasForeignKey(x => x.ComponentProductId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<DealerType>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Dealer>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();       // mã đại lý duy nhất theo tenant
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId);
            e.HasOne(x => x.DealerType).WithMany().HasForeignKey(x => x.DealerTypeId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TraceTemplate>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<TraceTemplateCte>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.Code }).IsUnique();   // 1 mã CTE chỉ xuất hiện 1 lần trong 1 mẫu
            e.HasOne(x => x.Template).WithMany(x => x.Ctes).HasForeignKey(x => x.TemplateId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TraceTemplateKde>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.Code }).IsUnique();   // 1 mã KDE chỉ xuất hiện 1 lần trong 1 mẫu
            e.HasOne(x => x.Template).WithMany(x => x.Kdes).HasForeignKey(x => x.TemplateId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TraceTemplateCteKde>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.CteCode, x.KdeCode }).IsUnique();   // 1 cặp CTE-KDE chỉ gán 1 lần
            e.HasOne(x => x.Template).WithMany(x => x.CteKdes).HasForeignKey(x => x.TemplateId);
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
