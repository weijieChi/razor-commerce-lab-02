namespace RazorCommerceLab.Domain;

/// <summary>
/// 商品 Entity。
///
/// EF Core 會根據這個 class 建立 Products 資料表的結構。
///
/// 可以先把 Entity 理解成：
///
/// C# class
///     ↓ EF Core
/// database table
///
/// Product object
///     ↓
/// Products table 裡的一筆 row
/// </summary>
public class Product
{
    /// <summary>
    /// 商品主鍵。
    ///
    /// EF Core convention 看到名稱是 Id，
    /// 本身就能推斷它是 Primary Key。
    ///
    /// 不過我們後面仍會用 Fluent API 明確設定，
    /// 目的是練習 Fluent API。
    /// </summary>
    public int Id { get; set; }


    /// <summary>
    /// 外部 DummyJSON 商品 ID。
    ///
    /// DummyJSON 匯入的商品會有這個 ID。
    ///
    /// 例如：
    ///
    /// DummyJSON product id = 10
    /// ExternalId = 10
    ///
    /// 但是後台自己建立的商品不是來自 DummyJSON，
    /// 因此 ExternalId 可以不存在，所以使用 int?。
    /// </summary>
    public int? ExternalId { get; set; }


    /// <summary>
    /// 商品名稱。
    /// </summary>
    public string Title { get; set; } = string.Empty;


    /// <summary>
    /// 商品描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;


    /// <summary>
    /// 商品價格。
    ///
    /// 金額使用 decimal，
    /// 不使用 float / double。
    /// </summary>
    public decimal Price { get; set; }


    /// <summary>
    /// 折扣百分比。
    ///
    /// DummyJSON 有 discountPercentage，
    /// 所以先保留這個欄位。
    ///
    /// 例如：
    ///
    /// 10.5
    /// → 10.5% 折扣
    /// </summary>
    public decimal DiscountPercentage { get; set; }


    /// <summary>
    /// 商品評分。
    /// </summary>
    public decimal Rating { get; set; }


    /// <summary>
    /// 商品庫存數量。
    /// </summary>
    public int Stock { get; set; }


    /// <summary>
    /// 商品品牌。
    ///
    /// 某些商品可能沒有品牌，
    /// 程式內先用空字串代表沒有資料。
    /// </summary>
    public string Brand { get; set; } = string.Empty;


    /// <summary>
    /// 商品分類，例如：
    ///
    /// beauty
    /// fragrances
    /// furniture
    /// groceries
    /// </summary>
    public string Category { get; set; } = string.Empty;


    /// <summary>
    /// 商品主要縮圖 URL。
    ///
    /// 如果沒有圖片，我們允許它保持空字串。
    ///
    /// Step 5 顯示商品時再判斷：
    ///
    /// 有 ThumbnailUrl
    /// → 使用商品圖片
    ///
    /// 沒有 ThumbnailUrl
    /// → 使用 picsum.photos 預設圖片
    ///
    /// 如果外部圖片載入失敗，
    /// → 再 fallback 到本機 product-placeholder.svg
    ///
    /// 因此「預設圖片」屬於畫面 fallback，
    /// 不需要把 Picsum URL 寫進每一筆資料。
    /// </summary>
    public string ThumbnailUrl { get; set; } = string.Empty;


    /// <summary>
    /// 商品其他圖片。
    ///
    /// DummyJSON 的 images 是 string[]，
    /// 但目前這個教學專案不另外建立 ProductImage table。
    ///
    /// 所以 Step 4 匯入時會把圖片陣列序列化成 JSON：
    ///
    /// [
    ///   "https://...",
    ///   "https://..."
    /// ]
    ///
    /// 再以字串存到 SQLite。
    /// </summary>
    public string ImageUrlsJson { get; set; } = "[]";


    /// <summary>
    /// 是否為首頁精選商品。
    ///
    /// Step 5 首頁會使用這個欄位查詢精選商品。
    /// </summary>
    public bool IsFeatured { get; set; }


    /// <summary>
    /// 商品建立時間。
    ///
    /// 這裡使用 UTC，
    /// 避免把伺服器所在時區直接寫進資料庫。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
