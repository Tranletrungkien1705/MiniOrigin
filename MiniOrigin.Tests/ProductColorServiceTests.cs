using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>
/// Test danh mục Màu sắc + gán màu cho sản phẩm (port từ Mst_PartColor / Mst_MapPartColor):
/// mã màu bắt buộc + duy nhất, tên EN/VN bắt buộc, chặn xoá khi còn gán,
/// và luật cốt lõi: mỗi sản phẩm chỉ có tối đa 1 màu mặc định.
/// </summary>
public class ProductColorServiceTests
{
    private static (AppDbContext db, IProductColorService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new ProductColorService(db), conn);
    }

    private static async Task<int> AddProduct(AppDbContext db, string code = "P1")
    {
        var p = new Product { Code = code, Name = "Gạch " + code };
        db.Products.Add(p); await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task CreateColor_RequiresCodeAndNames()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateColorAsync("", "White", "Trắng", true)).ok);
            Assert.False((await svc.CreateColorAsync("W", "", "Trắng", true)).ok);
            Assert.False((await svc.CreateColorAsync("W", "White", "", true)).ok);
            var (ok, _, id) = await svc.CreateColorAsync("trang-bong", "Glossy White", "Trắng bóng", true);
            Assert.True(ok);
            Assert.Equal("TRANG-BONG", (await svc.GetColorAsync(id))!.Code);   // chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task CreateColor_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateColorAsync("XAM", "Grey", "Xám", true);
            var (ok, msg, _) = await svc.CreateColorAsync("XAM", "Grey 2", "Xám 2", true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task DeleteColor_BlockedWhenMapped()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, colorId) = await svc.CreateColorAsync("TRANG", "White", "Trắng", true);
            var pid = await AddProduct(db);
            await svc.AssignAsync(pid, colorId, false);

            var (ok, msg) = await svc.DeleteColorAsync(colorId);
            Assert.False(ok);
            Assert.Contains("còn được gán", msg);
        }
    }

    [Fact]
    public async Task Assign_RejectsDuplicatePair()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, colorId) = await svc.CreateColorAsync("TRANG", "White", "Trắng", true);
            var pid = await AddProduct(db);
            Assert.True((await svc.AssignAsync(pid, colorId, false)).ok);
            var (ok, msg, _) = await svc.AssignAsync(pid, colorId, false);
            Assert.False(ok);
            Assert.Contains("đã được gán", msg);
        }
    }

    [Fact]
    public async Task Assign_RejectsInactiveColor()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, colorId) = await svc.CreateColorAsync("XAM", "Grey", "Xám", true);
            await svc.SetColorActiveAsync(colorId, false);
            var pid = await AddProduct(db);
            var (ok, msg, _) = await svc.AssignAsync(pid, colorId, false);
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);
        }
    }

    [Fact]
    public async Task Assign_OnlyOneDefaultColorPerProduct()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, white) = await svc.CreateColorAsync("TRANG", "White", "Trắng", true);
            var (_, _, grey) = await svc.CreateColorAsync("XAM", "Grey", "Xám", true);
            var pid = await AddProduct(db);

            await svc.AssignAsync(pid, white, true);   // mặc định
            await svc.AssignAsync(pid, grey, true);    // mặc định mới -> gỡ mặc định cũ

            var maps = await svc.ListMapsAsync(pid, null);
            Assert.Single(maps, m => m.Map.IsDefault);
            Assert.Equal(grey, maps.Single(m => m.Map.IsDefault).Map.ColorId);
        }
    }

    [Fact]
    public async Task SetMapDefault_SwitchesDefault()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, white) = await svc.CreateColorAsync("TRANG", "White", "Trắng", true);
            var (_, _, grey) = await svc.CreateColorAsync("XAM", "Grey", "Xám", true);
            var pid = await AddProduct(db);
            var (_, _, m1) = await svc.AssignAsync(pid, white, true);
            var (_, _, m2) = await svc.AssignAsync(pid, grey, false);

            Assert.True((await svc.SetMapDefaultAsync(m2, true)).ok);
            var maps = await svc.ListMapsAsync(pid, null);
            Assert.Single(maps, m => m.Map.IsDefault);
            Assert.Equal(m2, maps.Single(m => m.Map.IsDefault).Map.Id);
            Assert.False(maps.Single(m => m.Map.Id == m1).Map.IsDefault);
        }
    }

    [Fact]
    public async Task RemoveMap_DeletesLink()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, colorId) = await svc.CreateColorAsync("TRANG", "White", "Trắng", true);
            var pid = await AddProduct(db);
            var (_, _, mapId) = await svc.AssignAsync(pid, colorId, false);
            Assert.True((await svc.RemoveMapAsync(mapId)).ok);
            Assert.Empty(await svc.ListMapsAsync(pid, null));
        }
    }
}