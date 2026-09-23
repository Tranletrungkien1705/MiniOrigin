using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Thương hiệu (port từ Mst_Brand): mã bắt buộc + duy nhất, tên bắt buộc, cờ hoạt động, chặn xoá khi còn sản phẩm.</summary>
public class BrandServiceTests
{
    private static (AppDbContext db, IBrandService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new BrandService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Viglacera", true)).ok);
            Assert.False((await svc.CreateAsync("VIGLACERA", "", true)).ok);
            var (ok, _, id) = await svc.CreateAsync("viglacera", "Viglacera", true);
            Assert.True(ok);
            var b = await svc.GetAsync(id);
            Assert.Equal("VIGLACERA", b!.Code);   // mã được chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("SANFI", "Sanfi", true);
            var (ok, msg, _) = await svc.CreateAsync("SANFI", "Sanfi 2", true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("VIGLACERA", "Viglacera", true);
            await svc.CreateAsync("SANFI", "Sanfi", false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("san", null));
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenProductsReference()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, brandId) = await svc.CreateAsync("VIGLACERA", "Viglacera", true);
            var prod = new Product { Code = "P1", Name = "Gạch", BrandId = brandId };
            db.Products.Add(prod); await db.SaveChangesAsync();

            var (ok, msg) = await svc.DeleteAsync(brandId);
            Assert.False(ok);
            Assert.Contains("còn sản phẩm", msg);

            await svc.AssignProductAsync(prod.Id, null);
            Assert.True((await svc.DeleteAsync(brandId)).ok);
        }
    }

    [Fact]
    public async Task SetActive_TogglesFlag()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("SANFI", "Sanfi", true);
            await svc.SetActiveAsync(id, false);
            Assert.False((await svc.GetAsync(id))!.Active);
        }
    }
}
