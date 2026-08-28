using Microsoft.AspNetCore.Mvc;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;

namespace MiniOrigin.Controllers;

/// <summary>
/// API JSON cho SPA React. DTO phẳng. Dashboard cache Redis 30s theo tenant (X-Cache).
/// Truy xuất nguồn gốc theo lô (GS1): CTE + KDE động + GLN + phả hệ lô. Tra cứu công khai đệ quy theo mã lô.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiV1Controller(IOriginService svc, ICache cache, ITenantContext tenant) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var key = $"origin:dash:{tenant.OrgId}";
        var hit = await cache.GetAsync<DashDto>(key);
        if (hit != null) { Response.Headers["X-Cache"] = "HIT"; return Ok(hit); }
        var d = await svc.DashboardAsync();
        var dto = new DashDto(d.Lots, d.Events, d.Ctes, d.Glns,
            d.Recent.Select(l => new { l.Id, l.Code, l.ProductName, status = Ui.Lot(l.Status).text }).Cast<object>().ToList());
        await cache.SetAsync(key, dto, TimeSpan.FromSeconds(30));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }

    [HttpGet("ctes")]
    public async Task<IActionResult> Ctes()
        => Ok((await svc.CtesAsync()).Select(c => new
        {
            c.Id, c.Code, c.Name, c.Icon, c.Ordinal,
            kdes = c.Kdes.OrderBy(k => k.Ordinal).Select(k => new { k.Id, k.Key, k.Label, k.Unit, k.Required })
        }));

    [HttpPost("ctes")]
    public async Task<IActionResult> CreateCte([FromBody] CteReq r)
    {
        var (ok, msg, id) = await svc.CreateCteAsync(new Cte { Code = r.Code ?? "", Name = r.Name, Icon = r.Icon ?? "bi-record-circle", Ordinal = r.Ordinal });
        return ok ? Ok(new { id }) : BadRequest(new { error = msg });
    }

    [HttpPost("ctes/{id:int}/kdes")]
    public async Task<IActionResult> AddKde(int id, [FromBody] KdeReq r)
    {
        var (ok, msg) = await svc.AddKdeAsync(new KdeDef { CteId = id, Key = r.Key, Label = r.Label, Unit = r.Unit, Required = r.Required, Ordinal = r.Ordinal });
        return ok ? Ok(new { ok }) : BadRequest(new { error = msg });
    }

    [HttpGet("glns")]
    public async Task<IActionResult> Glns()
        => Ok((await svc.GlnsAsync()).Select(g => new { g.Id, g.Code, g.Name, type = Ui.GlnType(g.Type), typeCode = (int)g.Type, g.Address }));

    [HttpPost("glns")]
    public async Task<IActionResult> CreateGln([FromBody] GlnReq r)
    {
        var (ok, msg) = await svc.CreateGlnAsync(new Gln { Code = r.Code ?? "", Name = r.Name, Type = (GlnType)r.Type, Address = r.Address });
        return ok ? Ok(new { ok }) : BadRequest(new { error = msg });
    }

    [HttpGet("lots")]
    public async Task<IActionResult> Lots([FromQuery] string? q)
        => Ok((await svc.LotsAsync(q)).Select(l => new
        {
            l.Id, l.Code, l.ProductName, origin = l.OriginGln?.Name, l.Quantity, l.Unit,
            status = (int)l.Status, statusText = Ui.Lot(l.Status).text, statusCss = Ui.Lot(l.Status).css, l.CreatedAt, events = l.Events.Count
        }));

    [HttpGet("lots/{id:int}")]
    public async Task<IActionResult> Lot(int id)
    {
        var l = await svc.GetLotAsync(id);
        if (l == null) return NotFound(new { error = "Không tìm thấy lô." });
        var parents = await svc.ParentLotsAsync(id);
        return Ok(new
        {
            l.Id, l.Code, l.ProductName, origin = l.OriginGln?.Name, l.Quantity, l.Unit,
            status = (int)l.Status, statusText = Ui.Lot(l.Status).text,
            events = l.Events.OrderBy(e => e.Sequence).ThenBy(e => e.EventTime).Select(e => new { e.CteName, e.CteIcon, gln = e.GlnName, e.EventTime, e.Operator, e.Note, kde = e.KdeJson }),
            parents = parents.Select(p => new { p.Id, p.Code, p.ProductName })
        });
    }

    [HttpPost("lots")]
    public async Task<IActionResult> CreateLot([FromBody] LotReq r)
    {
        var (ok, msg, id) = await svc.CreateLotAsync(r.Code ?? "", r.ProductName, r.Unit, r.Quantity, r.OriginGlnId);
        return ok ? Ok(new { id }) : BadRequest(new { error = msg });
    }

    [HttpPost("lots/{id:int}/events")]
    public async Task<IActionResult> AddEvent(int id, [FromBody] EventReq r)
    {
        var (ok, msg) = await svc.AddEventAsync(id, r.CteId, r.GlnId, r.When == default ? DateTime.Now : r.When, r.Operator, r.Note, r.Kde ?? new());
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    [HttpPost("lots/{id:int}/link")]
    public async Task<IActionResult> Link(int id, [FromBody] LinkReq r)
    {
        var (ok, msg) = await svc.LinkLotAsync(id, r.ParentCode ?? "", r.Quantity);
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    // Tra cứu công khai đệ quy theo mã lô (kèm phả hệ nguyên liệu).
    [HttpGet("trace/{code}")]
    public async Task<IActionResult> Trace(string code)
    {
        var t = await svc.TraceByCodeAsync(code);
        return t == null ? NotFound(new { error = "Không tìm thấy mã lô." }) : Ok(MapTrace(t));
    }

    private static object MapTrace(LotTrace t) => new
    {
        code = t.Lot.Code, product = t.Lot.ProductName, origin = t.Lot.OriginGln?.Name,
        status = Ui.Lot(t.Lot.Status).text, depth = t.Depth,
        events = t.Events.Select(e => new
        {
            cte = e.Ev.CteName, gln = e.Ev.GlnName, when = e.Ev.EventTime, op = e.Ev.Operator,
            kdes = e.Kdes.Select(k => new { k.Label, k.Value, k.Unit })
        }),
        parents = t.Parents.Select(MapTrace)
    };
}

public record DashDto(int Lots, int Events, int Ctes, int Glns, List<object> Recent);

public class CteReq { public string Name { get; set; } = ""; public string? Code { get; set; } public string? Icon { get; set; } public int Ordinal { get; set; } }
public class KdeReq { public string Key { get; set; } = ""; public string Label { get; set; } = ""; public string? Unit { get; set; } public bool Required { get; set; } public int Ordinal { get; set; } }
public class GlnReq { public string Name { get; set; } = ""; public string? Code { get; set; } public int Type { get; set; } public string? Address { get; set; } }
public class LotReq { public string? Code { get; set; } public string ProductName { get; set; } = ""; public string? Unit { get; set; } public decimal Quantity { get; set; } public int? OriginGlnId { get; set; } }
public class EventReq { public int CteId { get; set; } public int? GlnId { get; set; } public DateTime When { get; set; } public string? Operator { get; set; } public string? Note { get; set; } public Dictionary<string, string>? Kde { get; set; } }
public class LinkReq { public string? ParentCode { get; set; } public decimal? Quantity { get; set; } }
