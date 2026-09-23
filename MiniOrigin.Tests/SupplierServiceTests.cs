using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Nhà cung cấp (port từ Mst_Supplier): mã bắt buộc + duy nhất, tên bắt buộc, SupType mặc định NORMAL, cờ hoạt động, lọc theo từ khoá/trạng thái, xoá.</summary>
public class SupplierServiceTests
{
    private static (AppDbContext db, ISupplierService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new SupplierService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "NCC A", true)).ok);
            Assert.False((await svc.CreateAsync("NCC001", "", true)).ok);
            var (ok, _, id) = await svc.CreateAsync("ncc001", "NCC A", true);
            Assert.True(ok);
            var s = await svc.GetAsync(id);
            Assert.Equal("NCC001", s!.Code);   // mã được chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_DefaultsTypeNormal()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("NCC001", "NCC A", true);
            var s = await svc.GetAsync(id);
            Assert.Equal("NORMAL", s!.Type);   // SupType luôn NORMAL khi tạo
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("NCC001", "NCC A", true);
            var (ok, msg, _) = await svc.CreateAsync("NCC001", "NCC B", true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("NCC001", "Vật liệu Miền Nam", true);
            await svc.CreateAsync("NCC002", "Men Gốm sứ", false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("men", null));
        }
    }

    [Fact]
    public async Task Update_RequiresName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("NCC001", "NCC A", true);
            Assert.False((await svc.UpdateAsync(id, "", true)).ok);
            Assert.True((await svc.UpdateAsync(id, "NCC A (mới)", true)).ok);
            Assert.Equal("NCC A (mới)", (await svc.GetAsync(id))!.Name);
        }
    }

    [Fact]
    public async Task SetActive_TogglesFlag()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("NCC001", "NCC A", true);
            await svc.SetActiveAsync(id, false);
            Assert.False((await svc.GetAsync(id))!.Active);
        }
    }

    [Fact]
    public async Task Delete_RemovesSupplier()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("NCC001", "NCC A", true);
            Assert.True((await svc.DeleteAsync(id)).ok);
            Assert.Null(await svc.GetAsync(id));
        }
    }
}
