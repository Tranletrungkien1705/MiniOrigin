namespace MiniOrigin.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum GlnType { Farm = 0, Factory = 1, Warehouse = 2, Store = 3, Transport = 4 }
public enum LotStatus { Open = 0, Shipped = 1, Sold = 2, Recalled = 3 }

public class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Global Location Number — địa điểm chuẩn GS1
public class Gln : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // 13 chữ số GLN
    public string Name { get; set; } = "";
    public GlnType Type { get; set; }
    public string? Address { get; set; }
}

// Critical Tracking Event — loại sự kiện (Trồng, Thu hoạch, Đóng gói, Vận chuyển…)
public class Cte : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "bi-record-circle";
    public int Ordinal { get; set; }
    public List<KdeDef> Kdes { get; set; } = new();
}

// Key Data Element — định nghĩa 1 trường dữ liệu thuộc 1 CTE
public class KdeDef : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int CteId { get; set; }
    public Cte? Cte { get; set; }
    public string Key { get; set; } = "";                // định danh (temperature)
    public string Label { get; set; } = "";              // hiển thị (Nhiệt độ)
    public string? Unit { get; set; }
    public bool Required { get; set; }
    public int Ordinal { get; set; }
}

public class Product : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
}

// Lô sản xuất — đơn vị truy xuất
public class Lot : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // Mã lô (GLOBAL unique — tra cứu công khai)
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public int? OriginGlnId { get; set; }
    public Gln? OriginGln { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public LotStatus Status { get; set; } = LotStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TraceEvent> Events { get; set; } = new();
}

// Phả hệ lô: lô con (thành phẩm) ← lô cha (nguyên liệu / bán thành phẩm)
public class LotLink : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int ChildLotId { get; set; }                  // lô kết quả
    public int ParentLotId { get; set; }                 // lô đầu vào
    public decimal? Quantity { get; set; }
}

// Sự kiện thực tế gắn với lô (1 CTE tại 1 GLN + giá trị KDE)
public class TraceEvent : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int LotId { get; set; }
    public Lot? Lot { get; set; }
    public int CteId { get; set; }
    public string CteName { get; set; } = "";
    public string CteIcon { get; set; } = "bi-record-circle";
    public int? GlnId { get; set; }
    public string? GlnName { get; set; }
    public DateTime EventTime { get; set; } = DateTime.Now;
    public string? Operator { get; set; }
    public string? Note { get; set; }
    public string KdeJson { get; set; } = "{}";          // {"temperature":"4","lot":"A1"}
    public int Sequence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
