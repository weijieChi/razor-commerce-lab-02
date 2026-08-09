namespace RazorCommerceLab.Domain;

/// <summary>
/// 匿名購物車裡的一筆商品。
///
/// 這個專案沒有登入系統，
/// 因此購物車不是使用 UserId 識別。
///
/// 我們之後會在 Browser Cookie 裡存一個 CartKey。
///
/// 例如：
///
/// Browser Cookie
/// razor_commerce_cart = abc-123
///
/// SQLite：
///
/// CartKey   ProductId   Quantity
/// abc-123       1           2
/// abc-123       8           1
///
/// 這樣 Server 就知道同一個匿名使用者的購物車內容。
/// </summary>
public class CartItem
{
    /// <summary>
    /// CartItem 本身的 Primary Key。
    /// </summary>
    public int Id { get; set; }


    /// <summary>
    /// 匿名購物車識別碼。
    ///
    /// Step 6 會從 Cookie：
    ///
    /// razor_commerce_cart
    ///
    /// 取得這個值。
    /// </summary>
    public string CartKey { get; set; } = string.Empty;


    /// <summary>
    /// Foreign Key。
    ///
    /// 指向 Product.Id。
    ///
    /// 例如 ProductId = 5：
    ///
    /// CartItems.ProductId
    ///          ↓
    /// Products.Id = 5
    /// </summary>
    public int ProductId { get; set; }


    /// <summary>
    /// Navigation Property。
    ///
    /// ProductId 是真正存進 CartItems table 的 FK。
    ///
    /// Product 則讓 EF Core 可以透過物件關係：
    ///
    /// cartItem.Product.Title
    ///
    /// 取得關聯商品。
    ///
    /// null! 表示：
    /// 「我知道建構物件當下它可能尚未初始化，
    ///   但 EF Core 載入關聯資料後會處理。」
    /// </summary>
    public Product Product { get; set; } = null!;


    /// <summary>
    /// 購買數量。
    /// </summary>
    public int Quantity { get; set; }


    /// <summary>
    /// 加入購物車時間。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    /// <summary>
    /// 最後修改數量的時間。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
