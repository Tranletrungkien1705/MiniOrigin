using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test Mẫu truy xuất (port từ Mst_TemplateNWType / TplNWT_Mst_CTE / TplNWT_Mst_KDE / TplNWT_CTE_KDE):
/// mã bắt buộc + duy nhất, tên bắt buộc, phải có ≥1 CTE/KDE/gán, mã con không trùng, gán phải tham chiếu
/// CTE/KDE có trong mẫu, vòng đời PENDING → APPROVE → CANCEL (chỉ PENDING mới sửa/xoá/duyệt/huỷ).</summary>
public class TraceTemplateServiceTests
{
    private static (AppDbContext db, ITraceTemplateService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new TraceTemplateService(db), conn);
    }

    private static List<TraceCteInput> Ctes() => new() { new TraceCteInput("HARVEST", "Thu hoạch", null) };
    private static List<TraceKdeInput> Kdes() => new() { new TraceKdeInput("FIELD", "Thửa ruộng", "TEXT", null, false, false) };
    private static List<TraceCteKdeInput> Maps() => new() { new TraceCteKdeInput("HARVEST", "FIELD", null, false, true) };

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Mẫu A", null, Ctes(), Kdes(), Maps())).ok);
            Assert.False((await svc.CreateAsync("FRUIT", "", null, Ctes(), Kdes(), Maps())).ok);
            var (ok, _, id) = await svc.CreateAsync("fruit", "Mẫu A", null, Ctes(), Kdes(), Maps());
            Assert.True(ok);
            Assert.Equal("FRUIT", (await svc.GetAsync(id))!.Template.Code);   // mã chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), Maps());
            var (ok, msg, _) = await svc.CreateAsync("FRUIT", "Mẫu B", null, Ctes(), Kdes(), Maps());
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Create_RequiresChildren()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("FRUIT", "Mẫu A", null, new(), Kdes(), Maps())).ok);   // thiếu CTE
            Assert.False((await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), new(), Maps())).ok);   // thiếu KDE
            Assert.False((await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), new())).ok);   // thiếu gán
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateChildCodes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var dupCte = new List<TraceCteInput> { new("HARVEST", "Thu hoạch", null), new("HARVEST", "Thu hoạch 2", null) };
            var (ok, msg, _) = await svc.CreateAsync("FRUIT", "Mẫu A", null, dupCte, Kdes(), Maps());
            Assert.False(ok);
            Assert.Contains("trùng", msg);
        }
    }

    [Fact]
    public async Task Create_RejectsMapToUnknownChild()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var badMap = new List<TraceCteKdeInput> { new("UNKNOWN", "FIELD", null, false, false) };
            var (ok, msg, _) = await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), badMap);
            Assert.False(ok);
            Assert.Contains("không có trong mẫu", msg);
        }
    }

    [Fact]
    public async Task Create_StartsPending()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), Maps());
            var d = await svc.GetAsync(id);
            Assert.Equal(TraceTemplateStatus.Pending, d!.Template.Status);
            Assert.Single(d.Ctes);
            Assert.Single(d.Kdes);
            Assert.Single(d.CteKdes);
        }
    }

    [Fact]
    public async Task Approve_ThenBlocksEditAndDelete()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), Maps());
            Assert.True((await svc.ApproveAsync(id)).ok);
            Assert.Equal(TraceTemplateStatus.Approve, (await svc.GetAsync(id))!.Template.Status);
            // đã duyệt → không sửa/xoá/duyệt lại được
            Assert.False((await svc.UpdateAsync(id, "Mẫu A2", null, Ctes(), Kdes(), Maps())).ok);
            Assert.False((await svc.DeleteAsync(id)).ok);
            Assert.False((await svc.ApproveAsync(id)).ok);
        }
    }

    [Fact]
    public async Task Cancel_ThenBlocksEdit()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), Maps());
            Assert.True((await svc.CancelAsync(id)).ok);
            Assert.Equal(TraceTemplateStatus.Cancel, (await svc.GetAsync(id))!.Template.Status);
            Assert.False((await svc.UpdateAsync(id, "Mẫu A2", null, Ctes(), Kdes(), Maps())).ok);
        }
    }

    [Fact]
    public async Task Update_ReplacesChildren()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("FRUIT", "Mẫu A", null, Ctes(), Kdes(), Maps());
            var newCtes = new List<TraceCteInput> { new("HARVEST", "Thu hoạch", null), new("PACK", "Đóng gói", null) };
            var newKdes = new List<TraceKdeInput> { new("FIELD", "Thửa ruộng", "TEXT", null, false, false), new("PACKSIZE", "Quy cách", "NUMBER", null, false, false) };
            var newMaps = new List<TraceCteKdeInput> { new("HARVEST", "FIELD", null, false, true), new("PACK", "PACKSIZE", null, false, false) };
            Assert.True((await svc.UpdateAsync(id, "Mẫu A", null, newCtes, newKdes, newMaps)).ok);
            var d = await svc.GetAsync(id);
            Assert.Equal(2, d!.Ctes.Count);
            Assert.Equal(2, d.Kdes.Count);
            Assert.Equal(2, d.CteKdes.Count);
        }
    }

    [Fact]
    public async Task List_FiltersByStatusAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id1) = await svc.CreateAsync("FRUIT", "Mẫu nông sản", null, Ctes(), Kdes(), Maps());
            await svc.CreateAsync("SEAFOOD", "Mẫu thuỷ sản", null, Ctes(), Kdes(), Maps());
            await svc.ApproveAsync(id1);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, TraceTemplateStatus.Approve));
            Assert.Single(await svc.ListAsync("thuỷ sản", null));
        }
    }
}