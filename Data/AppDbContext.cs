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
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<LotLink> LotLinks => Set<LotLink>();
    public DbSet<TraceEvent> Events => Set<TraceEvent>();

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
        b.Entity<Product>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
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
    }

    public override int SaveChanges() { StampOrg(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) { StampOrg(); return base.SaveChangesAsync(ct); }
    private void StampOrg()
    {
        foreach (var e in ChangeTracker.Entries<IOrgOwned>())
            if (e.State == EntityState.Added && e.Entity.OrgId == Guid.Empty) e.Entity.OrgId = _orgId;
    }
}
