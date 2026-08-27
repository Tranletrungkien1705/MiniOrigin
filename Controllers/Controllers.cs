using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;

namespace MiniOrigin.Controllers;

public class HomeController(IOriginService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Dash = await svc.DashboardAsync(); return View(); }
}

public class CteController(IOriginService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.CtesAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? code, string? icon)
    {
        var (ok, msg, id) = await svc.CreateCteAsync(new Cte { Name = name ?? "", Code = (code ?? "").Trim().ToUpper(), Icon = string.IsNullOrWhiteSpace(icon) ? "bi-record-circle" : icon });
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var c = await svc.GetCteAsync(id);
        return c == null ? NotFound() : View(c);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddKde(int id, string label, string? key, string? unit, bool required)
    {
        var ord = (await svc.GetCteAsync(id))?.Kdes.Count ?? 0;
        var (ok, msg) = await svc.AddKdeAsync(new KdeDef { CteId = id, Label = label ?? "", Key = (key ?? "").Trim(), Unit = unit, Required = required, Ordinal = ord + 1 });
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Detail), new { id });
    }
}

public class GlnController(IOriginService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.GlnsAsync());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? code, GlnType type, string? address)
    {
        var (ok, msg) = await svc.CreateGlnAsync(new Gln { Name = name ?? "", Code = (code ?? "").Trim(), Type = type, Address = address });
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Index));
    }
}

public class LotController(IOriginService svc) : Controller
{
    public async Task<IActionResult> Index(string? q) { ViewBag.Q = q; return View(await svc.LotsAsync(q)); }

    public async Task<IActionResult> Create() { ViewBag.Glns = await svc.GlnsAsync(); return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string productName, string? unit, decimal quantity, int? originGlnId)
    {
        var (ok, msg, id) = await svc.CreateLotAsync(code, productName, unit, quantity, originGlnId);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Create));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var lot = await svc.GetLotAsync(id);
        if (lot == null) return NotFound();
        ViewBag.Ctes = await svc.CtesAsync();
        ViewBag.Glns = await svc.GlnsAsync();
        ViewBag.Parents = await svc.ParentLotsAsync(id);
        return View(lot);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvent(int id, int cteId, int? glnId, DateTime eventTime, string? operatorName, string? note, [FromForm] Dictionary<string, string>? kde)
    {
        var (ok, msg) = await svc.AddEventAsync(id, cteId, glnId, eventTime, operatorName, note, kde ?? new());
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(int id, string parentCode, decimal? quantity)
    {
        var (ok, msg) = await svc.LinkLotAsync(id, parentCode, quantity);
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Detail), new { id });
    }
}

public class TraceController(IOriginService svc) : Controller
{
    [Route("Trace/{code?}")]
    public async Task<IActionResult> Index(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return View("Search");
        var t = await svc.TraceByCodeAsync(code);
        ViewBag.Code = code;
        if (t == null) { ViewBag.NotFound = true; return View("Search"); }
        return View(t);
    }
}

public class OrgController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        Request.Cookies.TryGetValue(TenantContext.CookieName, out var curKey);
        ViewBag.CurrentKey = curKey ?? TenantContext.DefaultApiKey;
        return View(await db.Orgs.IgnoreQueryFilters().OrderBy(o => o.CreatedAt).ToListAsync());
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Cần tên tổ chức."; return RedirectToAction(nameof(Index)); }
        var org = new Org { Name = name.Trim(), ApiKey = "origin_" + Guid.NewGuid().ToString("N") };
        db.Orgs.Add(org); await db.SaveChangesAsync();
        SetCookies(org.ApiKey, org.Name);
        TempData["Success"] = $"Đã tạo & chuyển sang \"{org.Name}\"."; return RedirectToAction("Index", "Home");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(string apiKey)
    {
        var org = await db.Orgs.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.ApiKey == apiKey);
        if (org == null) { TempData["Error"] = "Không tìm thấy."; return RedirectToAction(nameof(Index)); }
        SetCookies(org.ApiKey, org.Name); return RedirectToAction("Index", "Home");
    }
    private void SetCookies(string k, string n)
    {
        var o = new CookieOptions { IsEssential = true, Expires = DateTimeOffset.UtcNow.AddDays(30) };
        Response.Cookies.Append(TenantContext.CookieName, k, o); Response.Cookies.Append("org_name", n, o);
    }
}
