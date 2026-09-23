using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test Định mức nguyên vật liệu (BOM) — port từ Mst_BOM / Mst_BOMDtl / Mst_BOMType:
/// mã BOM bắt buộc + duy nhất, sản phẩm cha & loại BOM phải tồn tại (loại phải hoạt động),
/// BOM phải có ≥1 dòng, Qty ≥ 0, chặn trùng thành phần & vòng lặp A→A / A→B,B→A,
/// mỗi sản phẩm cha chỉ 1 BOM mặc định đã duyệt, vòng đời Pending→Approve→Finish.</summary>
public class BomServiceTests
{
    private static (AppDbContext db, IBomService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new BomService(db), conn);
    }

    private static async Task<(Product parent, Product comp, BomType type)> Seed(AppDbContext db)
    {
        var parent = new Product { Code = "P-PARENT", Name = "Sản phẩm cha" };
        var comp = new Product { Code = "P-COMP", Name = "Thành phần" };
        db.Products.AddRange(parent, comp);
        var type = new BomType { Code = "SANXUAT", Description = "BOM sản xuất", Active = true };
        db.BomTypes.Add(type);
        await db.SaveChangesAsync();
        return (parent, comp, type);
    }

    [Fact]
    public async Task Create_RequiresCodeAndLines()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            Assert.False((await svc.CreateAsync("", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") })).ok);
            Assert.False((await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new())).ok);   // không có dòng
            var (ok, _, id) = await svc.CreateAsync("bom1", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") });
            Assert.True(ok);
            Assert.Equal("BOM1", (await svc.GetAsync(id))!.Bom.Code);   // chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") });
            var (ok, msg, _) = await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new() { new(comp.Id, 2, "kg") });
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Create_RequiresExistingActiveType()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, _) = await Seed(db);
            Assert.False((await svc.CreateAsync("BOM1", parent.Id, 999, false, null, new() { new(comp.Id, 1, "kg") })).ok);
            var (_, _, tid) = await svc.CreateTypeAsync("OFF", "ngừng", false);
            var (ok, msg, _) = await svc.CreateAsync("BOM1", parent.Id, tid, false, null, new() { new(comp.Id, 1, "kg") });
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);
        }
    }

    [Fact]
    public async Task Create_RejectsNegativeQtyAndDuplicateComponent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            Assert.False((await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new() { new(comp.Id, -1, "kg") })).ok);
            var (ok, msg, _) = await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null,
                new() { new(comp.Id, 1, "kg"), new(comp.Id, 2, "kg") });
            Assert.False(ok);
            Assert.Contains("lặp", msg);
        }
    }

    [Fact]
    public async Task Create_RejectsSelfAndDirectCycle()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            // A → A
            var (ok1, msg1, _) = await svc.CreateAsync("BOM-A", parent.Id, type.Id, false, null, new() { new(parent.Id, 1, "kg") });
            Assert.False(ok1);
            Assert.Contains("chính sản phẩm cha", msg1);

            // A → B (parent chứa comp)
            Assert.True((await svc.CreateAsync("BOM-A", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") })).ok);
            // B → A (comp chứa parent) → chu trình trực tiếp
            var (ok2, msg2, _) = await svc.CreateAsync("BOM-B", comp.Id, type.Id, false, null, new() { new(parent.Id, 1, "kg") });
            Assert.False(ok2);
            Assert.Contains("chu trình", msg2);
        }
    }

    [Fact]
    public async Task DefaultFlag_OnlyOneApprovedDefaultPerParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            var (_, _, id1) = await svc.CreateAsync("BOM1", parent.Id, type.Id, true, null, new() { new(comp.Id, 1, "kg") });
            Assert.True((await svc.ApproveAsync(id1)).ok);

            // BOM thứ 2 cũng đánh dấu mặc định cho cùng sản phẩm cha → chặn khi duyệt
            var (_, _, id2) = await svc.CreateAsync("BOM2", parent.Id, type.Id, true, null, new() { new(comp.Id, 2, "kg") });
            var (ok, msg) = await svc.ApproveAsync(id2);
            Assert.False(ok);
            Assert.Contains("mặc định", msg);
        }
    }

    [Fact]
    public async Task Lifecycle_PendingApproveFinish()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            var (_, _, id) = await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") });
            Assert.Equal(BomStatus.Pending, (await svc.GetAsync(id))!.Bom.Status);

            Assert.True((await svc.ApproveAsync(id)).ok);
            Assert.Equal(BomStatus.Approve, (await svc.GetAsync(id))!.Bom.Status);

            // Không sửa/xoá được khi đã duyệt
            Assert.False((await svc.UpdateAsync(id, false, "x")).ok);
            Assert.False((await svc.DeleteAsync(id)).ok);

            Assert.True((await svc.FinishAsync(id)).ok);
            Assert.Equal(BomStatus.Finish, (await svc.GetAsync(id))!.Bom.Status);
        }
    }

    [Fact]
    public async Task DeleteType_BlockedWhenBomsReference()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (parent, comp, type) = await Seed(db);
            await svc.CreateAsync("BOM1", parent.Id, type.Id, false, null, new() { new(comp.Id, 1, "kg") });
            var (ok, msg) = await svc.DeleteTypeAsync(type.Id);
            Assert.False(ok);
            Assert.Contains("còn BOM", msg);
        }
    }
}
