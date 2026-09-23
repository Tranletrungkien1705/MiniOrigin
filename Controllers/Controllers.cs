using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;

namespace MiniOrigin.Controllers;

public class HomeController : Controller
{
    // SPA React (admin) ở "/". Trang tra cứu công khai /Trace (Razor) giữ nguyên.
    public IActionResult Index() => Redirect("/index.html");
}

public class LegacyController(IOriginService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Dash = await svc.DashboardAsync(); return View("~/Views/Home/Index.cshtml"); }
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

public class BrandController(IBrandService svc) : Controller
{
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class WarrantyTypeController(IWarrantyTypeService svc, IOriginService origin) : Controller
{
    // Danh mục Loại thời hạn bảo hành — port từ Mst_PartWarrantyType của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        ViewBag.Products = await origin.ProductsAsync();
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, string? remark, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, string? remark, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int productId, int? warrantyTypeId)
    {
        var (ok, msg) = await svc.AssignProductAsync(productId, warrantyTypeId);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class MaterialTypeController(IMaterialTypeService svc, IOriginService origin) : Controller
{
    // Danh mục Nhóm vật liệu (loại vật liệu) — port từ Mst_PartMaterialType của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        ViewBag.Products = await origin.ProductsAsync();
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int productId, int? materialTypeId)
    {
        var (ok, msg) = await svc.AssignProductAsync(productId, materialTypeId);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class PartTypeController(IPartTypeService svc, IOriginService origin) : Controller
{
    // Danh mục Loại sản phẩm — port từ Mst_PartType của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        ViewBag.Products = await origin.ProductsAsync();
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int productId, int? partTypeId)
    {
        var (ok, msg) = await svc.AssignProductAsync(productId, partTypeId);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class PartUnitController(IPartUnitService svc) : Controller
{
    // Danh mục Đơn vị tính (master) — port từ Mst_PartUnit của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, bool isStandard = false, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, isStandard, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, bool isStandard, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, isStandard, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class SupplierController(ISupplierService svc) : Controller
{
    // Danh mục Nhà cung cấp (master) — port từ Mst_Supplier của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
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

public class AuthenticityController(IAuthenticityService svc, IOriginService origin) : Controller
{
    // Trang xác thực công khai: nhập serial + mã bí mật.
    [Route("Verify/{serial?}")]
    public async Task<IActionResult> Index(string? serial)
    {
        ViewBag.Serial = serial;
        if (!string.IsNullOrWhiteSpace(serial)) ViewBag.Lookup = await svc.LookupAsync(serial);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(string serial, string pin)
    {
        var res = await svc.VerifyAsync(serial ?? "", pin ?? "");
        ViewBag.Serial = serial; ViewBag.Result = res;
        ViewBag.Lookup = await svc.LookupAsync(serial ?? "");
        return View(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string serial, string pin, string? customerName, string? phone, string? address)
    {
        var res = await svc.ActivateAsync(serial ?? "", pin ?? "", customerName, phone, address);
        TempData[res.Ok ? "Success" : "Error"] = res.Message;
        return RedirectToAction(nameof(Index), new { serial });
    }

    // Quản trị danh sách đơn vị sản phẩm.
    public async Task<IActionResult> Units(string? q)
    {
        ViewBag.Q = q;
        ViewBag.Products = await origin.ProductsAsync();
        return View(await svc.ListAsync(q));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string serial, string pin, int? productId, int? brandId, string? lotCode, string? origin, int warrantyMonths = 12)
    {
        var (ok, msg, _) = await svc.CreateAsync(serial, pin, productId, brandId, lotCode, origin, warrantyMonths);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Units));
    }
}

public class VerifyBatchController(IVerifyBatchService svc) : Controller
{
    // Quản trị lần xuất ghép (batch xác thực) — port từ Inv_VerifiedIDInOut của InBrand.
    public async Task<IActionResult> Index(string? q)
    {
        ViewBag.Q = q;
        return View(await svc.ListAsync(q));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var s = await svc.GetAsync(id);
        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string productName, string? refNo, string? refNoSys,
        string? transportType, string? plateNo, string? receivePlace, string? invOutType, int qtyPlan)
    {
        var (ok, msg, id) = await svc.CreateAsync(productName, refNo, refNoSys, transportType, plateNo, receivePlace, invOutType, qtyPlan);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(int id, string idNo, string? pin, string? boxNo, string? customerName)
    {
        var (ok, msg, _) = await svc.ScanAsync(id, idNo, pin, boxNo, customerName);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(int id)
    {
        var (ok, msg) = await svc.MergeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, msg) = await svc.CancelAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }
}

public class PackingController(IPackingService svc) : Controller
{
    // Quản trị đóng hộp / đóng thùng — port từ module Box/Can của InBrand.
    public async Task<IActionResult> Index(string? q)
    {
        ViewBag.Q = q;
        ViewBag.Cans = await svc.ListCansAsync(null);
        return View(await svc.ListBoxesAsync(q));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var s = await svc.GetBoxAsync(id);
        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string? secretNo, string? remark)
    {
        var (ok, msg, id) = await svc.CreateBoxAsync(code, secretNo, remark);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pack(int id, string serialNo)
    {
        var (ok, msg) = await svc.PackSerialAsync(id, serialNo);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpack(int id, string serialNo)
    {
        var (ok, msg) = await svc.UnpackSerialAsync(id, serialNo);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PackIntoCan(int id, string boxCode)
    {
        var (ok, msg) = await svc.PackBoxAsync(id, boxCode);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCan(string code, string? secretNo, string? remark)
    {
        var (ok, msg, _) = await svc.CreateCanAsync(code, secretNo, remark);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Tra cứu công khai theo mã hộp (giống BoxController của InBrand).
    [Route("Box/{code?}")]
    public async Task<IActionResult> Lookup(string? code)
    {
        ViewBag.Code = code;
        if (!string.IsNullOrWhiteSpace(code)) ViewBag.Lookup = await svc.LookupBoxAsync(code);
        return View();
    }
}

public class SearchLogController(ISearchLogService svc) : Controller
{
    // Lịch sử tra cứu — port từ Rpt_SearchHis của InBrand.
    public async Task<IActionResult> Index(string? q, SearchType? type)
    {
        ViewBag.Q = q; ViewBag.Type = type;
        ViewBag.Stats = await svc.StatsAsync();
        return View(await svc.ListAsync(q, type));
    }
}

public class BomController(IBomService svc, IOriginService origin) : Controller
{
    // Định mức nguyên vật liệu (Bill of Materials) — port từ Mst_BOM / Mst_BOMDtl / Mst_BOMType của InBrand.
    public async Task<IActionResult> Index(string? q, BomStatus? status)
    {
        ViewBag.Q = q; ViewBag.Status = status;
        ViewBag.Types = await svc.ListTypesAsync(null, null);
        ViewBag.Products = await origin.ProductsAsync();
        return View(await svc.ListAsync(q, status));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var d = await svc.GetAsync(id);
        return d == null ? NotFound() : View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, int parentProductId, int bomTypeId, bool isDefault, string? remark,
        int[]? componentProductId, decimal[]? qty, string[]? unit)
    {
        var lines = new List<BomLineInput>();
        if (componentProductId != null)
            for (int i = 0; i < componentProductId.Length; i++)
            {
                if (componentProductId[i] <= 0) continue;
                var q = (qty != null && i < qty.Length) ? qty[i] : 0;
                var u = (unit != null && i < unit.Length) ? unit[i] : null;
                lines.Add(new BomLineInput(componentProductId[i], q, u));
            }
        var (ok, msg, id) = await svc.CreateAsync(code, parentProductId, bomTypeId, isDefault, remark, lines);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, bool isDefault, string? remark)
    {
        var (ok, msg) = await svc.UpdateAsync(id, isDefault, remark);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var (ok, msg) = await svc.ApproveAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(int id)
    {
        var (ok, msg) = await svc.FinishAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateType(string code, string? description, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateTypeAsync(code, description, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteType(int id)
    {
        var (ok, msg) = await svc.DeleteTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class DealerController(IDealerService svc) : Controller
{
    // Danh mục Đại lý (nguồn gốc thương hiệu) — port từ Mst_Dealer / Mst_DealerType của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        ViewBag.Types = await svc.ListTypesAsync(null, null);
        ViewBag.Dealers = await svc.ListAsync(null, true);
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, parentId, dealerTypeId, invCode, materialTypeCode, skycicSiteId, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, parentId, dealerTypeId, invCode, materialTypeCode, skycicSiteId, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateType(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateTypeAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteType(int id)
    {
        var (ok, msg) = await svc.DeleteTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class InventoryController(IInventoryService svc) : Controller
{
    // Danh mục Kho hàng (nguồn gốc thương hiệu) — port từ Mst_Inventory / Mst_InventoryType / Mst_InventoryLevelType của InBrand.
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        ViewBag.Q = q; ViewBag.Active = active;
        ViewBag.Types = await svc.ListTypesAsync(null, null);
        ViewBag.LevelTypes = await svc.ListLevelTypesAsync(null, null);
        ViewBag.Inventories = await svc.ListAsync(null, true);
        return View(await svc.ListAsync(q, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateAsync(code, name, parentId, typeId, levelTypeId,
            address, contactName, contactPhone, contactEmail, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active)
    {
        var (ok, msg) = await svc.UpdateAsync(id, name, parentId, typeId, levelTypeId,
            address, contactName, contactPhone, contactEmail, remark, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var (ok, msg) = await svc.SetActiveAsync(id, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateType(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateTypeAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteType(int id)
    {
        var (ok, msg) = await svc.DeleteTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLevelType(string code, string name, bool active = true)
    {
        var (ok, msg, _) = await svc.CreateLevelTypeAsync(code, name, active);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLevelType(int id)
    {
        var (ok, msg) = await svc.DeleteLevelTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class TraceTemplateController(ITraceTemplateService svc) : Controller
{
    // Mẫu truy xuất (TemplateNWType) — port từ Mst_TemplateNWType / TplNWT_Mst_CTE / TplNWT_Mst_KDE / TplNWT_CTE_KDE của InBrand.
    public async Task<IActionResult> Index(string? q, TraceTemplateStatus? status)
    {
        ViewBag.Q = q; ViewBag.Status = status;
        return View(await svc.ListAsync(q, status));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var d = await svc.GetAsync(id);
        return d == null ? NotFound() : View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, string? remark,
        string[]? cteCode, string[]? cteName, string[]? cteApiLink,
        string[]? kdeCode, string[]? kdeName, string[]? kdeDataType, string[]? kdeRefNoList, string[]? kdeFlagList, string[]? kdeFlagQuery,
        string[]? mapCte, string[]? mapKde, string[]? mapApiLink, string[]? mapOsOrgView, string[]? mapKey)
    {
        var ctes = BuildCtes(cteCode, cteName, cteApiLink);
        var kdes = BuildKdes(kdeCode, kdeName, kdeDataType, kdeRefNoList, kdeFlagList, kdeFlagQuery);
        var maps = BuildMaps(mapCte, mapKde, mapApiLink, mapOsOrgView, mapKey);
        var (ok, msg, id) = await svc.CreateAsync(code, name, remark, ctes, kdes, maps);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, string? remark,
        string[]? cteCode, string[]? cteName, string[]? cteApiLink,
        string[]? kdeCode, string[]? kdeName, string[]? kdeDataType, string[]? kdeRefNoList, string[]? kdeFlagList, string[]? kdeFlagQuery,
        string[]? mapCte, string[]? mapKde, string[]? mapApiLink, string[]? mapOsOrgView, string[]? mapKey)
    {
        var ctes = BuildCtes(cteCode, cteName, cteApiLink);
        var kdes = BuildKdes(kdeCode, kdeName, kdeDataType, kdeRefNoList, kdeFlagList, kdeFlagQuery);
        var maps = BuildMaps(mapCte, mapKde, mapApiLink, mapOsOrgView, mapKey);
        var (ok, msg) = await svc.UpdateAsync(id, name, remark, ctes, kdes, maps);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var (ok, msg) = await svc.ApproveAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, msg) = await svc.CancelAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    private static List<TraceCteInput> BuildCtes(string[]? code, string[]? name, string[]? apiLink)
    {
        var list = new List<TraceCteInput>();
        if (code == null) return list;
        for (int i = 0; i < code.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(code[i])) continue;
            list.Add(new TraceCteInput(code[i], name != null && i < name.Length ? name[i] : "",
                apiLink != null && i < apiLink.Length ? apiLink[i] : null));
        }
        return list;
    }

    private static List<TraceKdeInput> BuildKdes(string[]? code, string[]? name, string[]? dataType, string[]? refNoList, string[]? flagList, string[]? flagQuery)
    {
        var list = new List<TraceKdeInput>();
        if (code == null) return list;
        for (int i = 0; i < code.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(code[i])) continue;
            list.Add(new TraceKdeInput(code[i], name != null && i < name.Length ? name[i] : "",
                dataType != null && i < dataType.Length ? dataType[i] : null,
                refNoList != null && i < refNoList.Length ? refNoList[i] : null,
                flagList != null && i < flagList.Length && flagList[i] == "true",
                flagQuery != null && i < flagQuery.Length && flagQuery[i] == "true"));
        }
        return list;
    }

    private static List<TraceCteKdeInput> BuildMaps(string[]? cte, string[]? kde, string[]? apiLink, string[]? osOrgView, string[]? key)
    {
        var list = new List<TraceCteKdeInput>();
        if (cte == null) return list;
        for (int i = 0; i < cte.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(cte[i])) continue;
            list.Add(new TraceCteKdeInput(cte[i], kde != null && i < kde.Length ? kde[i] : "",
                apiLink != null && i < apiLink.Length ? apiLink[i] : null,
                osOrgView != null && i < osOrgView.Length && osOrgView[i] == "true",
                key != null && i < key.Length && key[i] == "true"));
        }
        return list;
    }
}

public class OrgController(AppDbContext db) : Controller{
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
