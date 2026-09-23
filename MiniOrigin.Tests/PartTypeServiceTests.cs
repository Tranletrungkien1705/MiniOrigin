using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Loại sản phẩm (port từ Mst_PartType): mã bắt buộc + duy nhất, tên bắt buộc, cờ hoạt động, gán sản phẩm (loại phải tồn tại & hoạt động), chặn xoá khi còn sản phẩm.</summary>
public class PartTypeServiceTests
{
    private static (AppDbContext db, IPartTypeService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new PartTypeService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Sứ vệ sinh", true)).ok);
            Assert.False((await svc.CreateAsync("SUVIETRI", "", true)).ok);
            var (ok, _, id) = await svc.CreateAsync("suvietri", "Sứ vệ sinh", true);
            Assert.True(ok);
            var t = await svc.GetAsync(id);
            Assert.Equal("SUVIETRI", t!.Code);   // mã được chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("SUVIETRI", "Sứ vệ sinh", true);
            var (ok, msg, _) = await svc.CreateAsync("SUVIETRI", "Sứ vệ sinh (2)", true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("SUVIETRI", "Sứ vệ sinh", true);
            await svc.CreateAsync("SENVOI", "Sen vòi", false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("senvoi", null));
        }
    }

    [Fact]
    public async Task Assign_RequiresExistingActiveType()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var prod = new Product { Code = "P1", Name = "Gạch" };
            db.Products.Add(prod); await db.SaveChangesAsync();

            Assert.False((await svc.AssignProductAsync(prod.Id, 999)).ok);   // không tồn tại

            var (_, _, id) = await svc.CreateAsync("SENVOI", "Sen vòi", false);
            var (ok, msg) = await svc.AssignProductAsync(prod.Id, id);
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);

            await svc.SetActiveAsync(id, true);
            Assert.True((await svc.AssignProductAsync(prod.Id, id)).ok);
            Assert.Equal(id, (await db.Products.FindAsync(prod.Id))!.PartTypeId);
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenProductsReference()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("SUVIETRI", "Sứ vệ sinh", true);
            var prod = new Product { Code = "P1", Name = "Gạch", PartTypeId = id };
            db.Products.Add(prod); await db.SaveChangesAsync();

            var (ok, msg) = await svc.DeleteAsync(id);
            Assert.False(ok);
            Assert.Contains("còn sản phẩm", msg);

            await svc.AssignProductAsync(prod.Id, null);
            Assert.True((await svc.DeleteAsync(id)).ok);
        }
    }

    [Fact]
    public async Task SetActive_TogglesFlag()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("GACHLATNEN", "Gạch lát nền", true);
            await svc.SetActiveAsync(id, false);
            Assert.False((await svc.GetAsync(id))!.Active);
        }
    }
}
