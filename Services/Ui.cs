using MiniOrigin.Models;
namespace MiniOrigin.Services;

public static class Ui
{
    public static string GlnType(GlnType t) => t switch
    {
        Models.GlnType.Farm => "Trang trại", Models.GlnType.Factory => "Nhà máy",
        Models.GlnType.Warehouse => "Kho", Models.GlnType.Store => "Cửa hàng",
        Models.GlnType.Transport => "Vận chuyển", _ => t.ToString()
    };
    public static string GlnIcon(GlnType t) => t switch
    {
        Models.GlnType.Farm => "bi-tree", Models.GlnType.Factory => "bi-buildings",
        Models.GlnType.Warehouse => "bi-box-seam", Models.GlnType.Store => "bi-shop",
        Models.GlnType.Transport => "bi-truck", _ => "bi-geo-alt"
    };
    public static (string text, string css) Lot(LotStatus s) => s switch
    {
        LotStatus.Open => ("Đang mở", "primary"), LotStatus.Shipped => ("Đã xuất", "info"),
        LotStatus.Sold => ("Đã bán", "success"), LotStatus.Recalled => ("Thu hồi", "danger"),
        _ => (s.ToString(), "secondary")
    };
}
