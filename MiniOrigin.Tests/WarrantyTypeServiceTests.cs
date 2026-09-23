using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Loại thời hạn bảo hành (port từ Mst_PartWarrantyType): mã bắt buộc + duy nhất, tên bắt buộc, cờ hoạt động, gán sản phẩm (loại phải tồn tại & hoạt động), chặn xoá khi còn sản phẩm.</summary>
public class WarrantyTypeServiceTests
{
    private static (AppDbContext db, IWarrantyTypeService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new WarrantyTypeService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Bảo hành 10 năm", null, true)).ok);
            Assert.False((await svc.CreateAsync("A10", "", null, true)).ok);
            var (ok, _, id) = await svc.CreateAsync("a10", "Bảo hành 10 năm", "Sứ vệ sinh", true);
            Assert.True(ok);
            var t = await svc.GetAsync(id);
            Assert.Equal("A10", t!.Code);   // mã được chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("A10", "Bảo hành 10 năm", null, true);
            var (ok, msg, _) = await svc.CreateAsync("A10", "Bảo hành 10 năm (2)", null, true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("A10", "Bảo hành 10 năm", null, true);
            await svc.CreateAsync("A70", "Bảo hành 7 năm", null, false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("a70", null));
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

            var (_, _, id) = await svc.CreateAsync("A70", "Bảo hành 7 năm", null, false);
            var (ok, msg) = await svc.AssignProductAsync(prod.Id, id);
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);

            await svc.SetActiveAsync(id, true);
            Assert.True((await svc.AssignProductAsync(prod.Id, id)).ok);
            Assert.Equal(id, (await db.Products.FindAsync(prod.Id))!.WarrantyTypeId);
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenProductsReference()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("A10", "Bảo hành 10 năm", null, true);
            var prod = new Product { Code = "P1", Name = "Gạch", WarrantyTypeId = id };
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
            var (_, _, id) = await svc.CreateAsync("A20", "Bảo hành 20 năm", null, true);
            await svc.SetActiveAsync(id, false);
            Assert.False((await svc.GetAsync(id))!.Active);
        }
    }
}