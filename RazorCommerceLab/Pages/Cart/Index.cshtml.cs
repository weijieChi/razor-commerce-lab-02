using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Cart;

public class IndexModel : PageModel
{
    /*
        必須和 Step 6 CartEndpoints 使用同一個 Cookie 名稱。

        Cookie 內容不是商品資料，
        而只是匿名購物車的 CartKey。
    */
    private const string CartCookieName =
        "razor_commerce_cart";


    /*
        AppDbContext 由 ASP.NET Core DI 注入。

        不需要自己：

        new AppDbContext(...)
    */
    private readonly AppDbContext _db;


    // ==========================================
    // View data
    // ==========================================

    /*
        Razor View 要顯示的購物車商品。

        不直接把 CartItem Entity 丟給 View，
        而是整理成這個頁面真正需要的資料。
    */
    public IReadOnlyList<CartPageItem> Items
    { get; private set; } = [];


    /*
        商品總數量。

        例如：

        商品 A × 2
        商品 B × 3

        TotalQuantity = 5
    */
    public int TotalQuantity
    { get; private set; }


    /*
        購物車總金額。
    */
    public decimal TotalAmount
    { get; private set; }


    public IndexModel(AppDbContext db)
    {
        _db = db;
    }


    // ==========================================
    // GET /Cart
    // ==========================================

    /// <summary>
    /// 顯示購物車。
    /// </summary>
    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        /*
            GET 頁面只需要：

            1. 找 CartKey
            2. 查 CartItems
            3. 計算總數量與總金額
            4. Render HTML
        */

        await LoadCartAsync(
            cancellationToken);
    }


    // ==========================================
    // POST /Cart?handler=Update
    // ==========================================

    /// <summary>
    /// 更新購物車某項商品的數量。
    ///
    /// 對應：
    ///
    /// asp-page-handler="Update"
    /// </summary>
    public async Task<IActionResult> OnPostUpdateAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. Server-side validation
        // ======================================

        /*
            HTML 雖然等等會有：

            min="1"
            max="99"

            但瀏覽器資料不能完全相信。

            使用者仍然可以自己改 HTTP Request，
            所以 Server 仍然要檢查。
        */
        if (productId < 1)
        {
            TempData["StatusMessage"] =
                "商品編號無效。";

            return RedirectToPage();
        }


        if (quantity < 1 ||
            quantity > 99)
        {
            TempData["StatusMessage"] =
                "商品數量必須介於 1 到 99。";

            return RedirectToPage();
        }


        // ======================================
        // 2. 取得 CartKey
        // ======================================

        var cartKey =
            GetCartKey();


        /*
            Cookie 不存在：

            代表 Browser 尚未建立購物車。

            不需要建立新的購物車，
            因為這是一個 Update request。
        */
        if (cartKey is null)
        {
            TempData["StatusMessage"] =
                "目前沒有購物車。";

            return RedirectToPage();
        }


        // ======================================
        // 3. 找 CartItem + Product
        // ======================================

        /*
            Include(Product)：

            CartItem.ProductId
                   ↓
            Product.Id

            把 Product 一起載入，
            因為等等需要檢查 Product.Stock。
        */
        var cartItem =
            await _db.CartItems
                .Include(item =>
                    item.Product)

                .FirstOrDefaultAsync(
                    item =>
                        item.CartKey == cartKey &&
                        item.ProductId == productId,
                    cancellationToken);


        if (cartItem is null)
        {
            TempData["StatusMessage"] =
                "購物車中找不到這項商品。";

            return RedirectToPage();
        }


        // ======================================
        // 4. 檢查庫存
        // ======================================

        /*
            quantity 本身可能是合法的 1 ~ 99，

            但商品實際 Stock 可能只有 5。

            這種規則一定要查 DB 才能判斷。
        */
        if (quantity >
            cartItem.Product.Stock)
        {
            TempData["StatusMessage"] =
                $"數量不可超過目前庫存 {cartItem.Product.Stock}。";

            return RedirectToPage();
        }


        // ======================================
        // 5. 更新 Entity
        // ======================================

        cartItem.Quantity =
            quantity;

        cartItem.UpdatedAt =
            DateTime.UtcNow;


        // ======================================
        // 6. UPDATE SQLite
        // ======================================

        await _db.SaveChangesAsync(
            cancellationToken);


        // ======================================
        // 7. 一次性提示訊息
        // ======================================

        TempData["StatusMessage"] =
            "購物車數量已更新。";


        // ======================================
        // 8. Redirect
        // ======================================

        /*
            不直接：

            return Page();

            而是 RedirectToPage()。

            流程：

            POST
              ↓
            更新 DB
              ↓
            Redirect
              ↓
            GET /Cart
              ↓
            重新查 DB
              ↓
            Render 最新購物車

            這樣重新整理瀏覽器時，
            不會再次提交上一個 POST。
        */
        return RedirectToPage();
    }


    // ==========================================
    // POST /Cart?handler=Remove
    // ==========================================

    /// <summary>
    /// 移除購物車中的單一商品。
    /// </summary>
    public async Task<IActionResult> OnPostRemoveAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        if (productId < 1)
        {
            TempData["StatusMessage"] =
                "商品編號無效。";

            return RedirectToPage();
        }


        var cartKey =
            GetCartKey();


        if (cartKey is null)
        {
            TempData["StatusMessage"] =
                "目前沒有購物車。";

            return RedirectToPage();
        }


        var cartItem =
            await _db.CartItems
                .FirstOrDefaultAsync(
                    item =>
                        item.CartKey == cartKey &&
                        item.ProductId == productId,
                    cancellationToken);


        if (cartItem is null)
        {
            TempData["StatusMessage"] =
                "購物車中找不到這項商品。";

            return RedirectToPage();
        }


        /*
            Remove()：

            EF Core 將 Entity state
            標記成 Deleted。
        */
        _db.CartItems.Remove(
            cartItem);


        /*
            SaveChangesAsync()
            才真正執行 SQL DELETE。
        */
        await _db.SaveChangesAsync(
            cancellationToken);


        TempData["StatusMessage"] =
            "商品已從購物車移除。";


        return RedirectToPage();
    }


    // ==========================================
    // POST /Cart?handler=Clear
    // ==========================================

    /// <summary>
    /// 清空整個購物車。
    /// </summary>
    public async Task<IActionResult> OnPostClearAsync(
        CancellationToken cancellationToken)
    {
        var cartKey =
            GetCartKey();


        /*
            沒有 Cookie 本來就是空購物車。

            仍然 Redirect 回頁面即可。
        */
        if (cartKey is null)
        {
            TempData["StatusMessage"] =
                "購物車目前是空的。";

            return RedirectToPage();
        }


        /*
            找出同一 CartKey 的所有 CartItem。
        */
        var cartItems =
            await _db.CartItems
                .Where(item =>
                    item.CartKey == cartKey)

                .ToListAsync(
                    cancellationToken);


        if (cartItems.Count > 0)
        {
            /*
                RemoveRange：

                一次標記多筆 Entity
                為 Deleted。
            */
            _db.CartItems.RemoveRange(
                cartItems);


            await _db.SaveChangesAsync(
                cancellationToken);
        }


        /*
            這裡跟 Step 6 API 一樣：

            清空 CartItems，
            但不刪掉 CartKey Cookie。

            Cookie 可以繼續代表
            同一個匿名購物車。
        */
        TempData["StatusMessage"] =
            "購物車已清空。";


        return RedirectToPage();
    }


    // ==========================================
    // Load Cart
    // ==========================================

    /// <summary>
    /// 從目前 Browser Cookie 的 CartKey
    /// 載入購物車內容。
    ///
    /// OnGetAsync() 使用。
    /// </summary>
    private async Task LoadCartAsync(
        CancellationToken cancellationToken)
    {
        var cartKey =
            GetCartKey();


        // ======================================
        // 沒有 Cookie → 空購物車
        // ======================================

        if (cartKey is null)
        {
            /*
                property 原本就已經是：

                Items = []
                TotalQuantity = 0
                TotalAmount = 0

                所以直接 return。
            */
            return;
        }


        // ======================================
        // 查 CartItems + Product
        // ======================================

        var cartItems =
            await _db.CartItems
                .AsNoTracking()

                .Include(item =>
                    item.Product)

                .Where(item =>
                    item.CartKey == cartKey)

                .OrderBy(item =>
                    item.Id)

                .ToListAsync(
                    cancellationToken);


        // ======================================
        // Entity → Page ViewModel
        // ======================================

        Items =
            cartItems
                .Select(item =>
                    new CartPageItem(
                        ProductId:
                            item.ProductId,

                        Title:
                            item.Product.Title,

                        Price:
                            item.Product.Price,

                        Quantity:
                            item.Quantity,

                        Stock:
                            item.Product.Stock,

                        ThumbnailUrl:
                            item.Product.ThumbnailUrl,

                        LineTotal:
                            item.Product.Price *
                            item.Quantity
                    ))

                .ToList();


        // ======================================
        // Total Quantity
        // ======================================

        TotalQuantity =
            Items.Sum(item =>
                item.Quantity);


        // ======================================
        // Total Amount
        // ======================================

        TotalAmount =
            Items.Sum(item =>
                item.LineTotal);
    }


    // ==========================================
    // Cookie
    // ==========================================

    /// <summary>
    /// 從 Request Cookies 取得匿名購物車 CartKey。
    ///
    /// 這裡只讀 Cookie，
    /// 不會建立新的 Cookie。
    /// </summary>
    private string? GetCartKey()
    {
        /*
            Request.Cookies：

            Browser Request
                  ↓
            razor_commerce_cart
                  ↓
            CartKey
        */
        if (Request.Cookies.TryGetValue(
                CartCookieName,
                out var cartKey) &&
            !string.IsNullOrWhiteSpace(
                cartKey))
        {
            return cartKey;
        }


        return null;
    }


    // ==========================================
    // Page ViewModel
    // ==========================================

    /// <summary>
    /// Cart Razor Page 顯示一筆商品時需要的資料。
    ///
    /// 它不是：
    ///
    /// CartItem Entity
    ///
    /// 也不是：
    ///
    /// CartItemDto API Response
    ///
    /// 它只屬於這個 Razor Page。
    /// </summary>
    public sealed record CartPageItem(
        int ProductId,
        string Title,
        decimal Price,
        int Quantity,
        int Stock,
        string ThumbnailUrl,
        decimal LineTotal
    );
}
