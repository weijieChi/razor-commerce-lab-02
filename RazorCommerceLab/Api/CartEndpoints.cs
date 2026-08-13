using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Api;


/// <summary>
/// 匿名購物車 Minimal API。
///
/// API：
///
/// GET    /api/cart
/// POST   /api/cart/items
/// PUT    /api/cart/items/{productId}
/// DELETE /api/cart/items/{productId}
/// DELETE /api/cart
/// </summary>
public static class CartEndpoints
{
    /*
        Cookie 名稱集中放在這裡。

        Browser 裡會看到：

        razor_commerce_cart=xxxxxxxx
    */
    private const string CartCookieName =
        "razor_commerce_cart";


    // ==========================================
    // Route registration
    // ==========================================

    public static IEndpointRouteBuilder MapCartEndpoints(
        this IEndpointRouteBuilder app)
    {
        /*
            所有購物車 API 都共用：

            /api/cart
        */
        var group =
            app.MapGroup("/api/cart");


        // GET /api/cart
        group.MapGet(
            "/",
            GetCartAsync);


        // POST /api/cart/items
        group.MapPost(
            "/items",
            AddCartItemAsync);


        // PUT /api/cart/items/1
        group.MapPut(
            "/items/{productId:int}",
            UpdateCartItemAsync);


        // DELETE /api/cart/items/1
        group.MapDelete(
            "/items/{productId:int}",
            RemoveCartItemAsync);


        // DELETE /api/cart
        group.MapDelete(
            "/",
            ClearCartAsync);


        return app;
    }


    // ==========================================
    // GET /api/cart
    // ==========================================

    /// <summary>
    /// 取得目前瀏覽器的購物車。
    /// </summary>
    private static async Task<IResult> GetCartAsync(
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        /*
            從 Request Cookie 取得 CartKey。

            如果連 Cookie 都沒有，
            代表這台瀏覽器還沒有購物車。

            GET 不需要因此建立一個新 CartKey，
            直接回空購物車即可。
        */
        if (!TryGetCartKey(
                httpContext,
                out var cartKey))
        {
            return Results.Ok(
                CreateEmptyCartResponse());
        }


        /*
            Cookie 存在：

            使用 CartKey 查 SQLite。
        */
        var cart =
            await BuildCartResponseAsync(
                db,
                cartKey,
                cancellationToken);

        return Results.Ok(cart);
    }


    // ==========================================
    // POST /api/cart/items
    // ==========================================

    /// <summary>
    /// 加入商品到購物車。
    ///
    /// Request：
    ///
    /// {
    ///   "productId": 1,
    ///   "quantity": 2
    /// }
    /// </summary>
    private static async Task<IResult> AddCartItemAsync(
        [FromBody] AddCartItemRequest request,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        /*
            到 handler 時：

            ProductId / Quantity
            已經經過 Minimal API Validation。

            所以這裡不需要再寫：

            if (request.Quantity <= 0)
        */


        // ======================================
        // 1. 確認商品存在
        // ======================================

        var product =
            await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    product =>
                        product.Id ==
                        request.ProductId,
                    cancellationToken);


        if (product is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"找不到 Id = {request.ProductId} 的商品。"
                });
        }


        // ======================================
        // 2. 檢查庫存
        // ======================================

        /*
            這已經不是 DTO Validation。

            因為必須查 DB 才知道 Product.Stock。
        */
        if (request.Quantity > product.Stock)
        {
            return Results.BadRequest(
                new
                {
                    message =
                        $"商品庫存不足，目前庫存為 {product.Stock}。"
                });
        }


        // ======================================
        // 3. 取得 / 建立 CartKey
        // ======================================

        /*
            如果使用者第一次加入購物車：

            Request 沒 Cookie
                ↓
            建立 Guid
                ↓
            Response Set-Cookie

            如果 Cookie 已經存在，
            就沿用同一個 CartKey。
        */
        var cartKey =
            GetOrCreateCartKey(httpContext);


        // ======================================
        // 4. 查詢購物車是否已有這項商品
        // ======================================

        /*
            Step 2 已經設計：

            CartKey + ProductId

            為 unique combination。

            所以同一個購物車的同一商品
            不建立兩筆資料。

            而是增加 Quantity。
        */
        var existingItem =
            await db.CartItems
                .SingleOrDefaultAsync(
                    item =>
                        item.CartKey == cartKey &&
                        item.ProductId ==
                            request.ProductId,
                    cancellationToken);


        // ======================================
        // 5A. 第一次加入
        // ======================================

        if (existingItem is null)
        {
            var now =
                DateTime.UtcNow;

            var cartItem =
                new CartItem
                {
                    CartKey = cartKey,

                    ProductId =
                        request.ProductId,

                    Quantity =
                        request.Quantity,

                    CreatedAt = now,
                    UpdatedAt = now
                };


            /*
                Add()：

                Entity state
                → Added

                還沒真正 INSERT。
            */
            db.CartItems.Add(cartItem);
        }

        // ======================================
        // 5B. 已經在購物車
        // ======================================

        else
        {
            /*
                例如：

                原本 Quantity = 2

                這次加入 Quantity = 3

                新數量 = 5
            */
            var newQuantity =
                existingItem.Quantity +
                request.Quantity;


            /*
                合併數量之後，
                再檢查一次 Stock。
            */
            if (newQuantity > product.Stock)
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            $"加入後數量會超過庫存，目前庫存為 {product.Stock}。"
                    });
            }


            /*
                existingItem 是 EF Core tracked Entity。

                修改 property 後，
                SaveChangesAsync()
                會產生 UPDATE。
            */
            existingItem.Quantity =
                newQuantity;

            existingItem.UpdatedAt =
                DateTime.UtcNow;
        }


        // ======================================
        // 6. 寫入 SQLite
        // ======================================

        await db.SaveChangesAsync(
            cancellationToken);


        // ======================================
        // 7. 回傳最新購物車
        // ======================================

        /*
            原本專案設計就是：

            加入購物車
            ↓
            回傳最新購物車 JSON

            Step 7 的 fetch
            就可以使用 TotalQuantity 等資訊。
        */
        var cart =
            await BuildCartResponseAsync(
                db,
                cartKey,
                cancellationToken);

        return Results.Ok(cart);
    }


    // ==========================================
    // PUT /api/cart/items/{productId}
    // ==========================================

    /// <summary>
    /// 更新某項商品的購物車數量。
    ///
    /// PUT /api/cart/items/5
    ///
    /// {
    ///   "quantity": 3
    /// }
    /// </summary>
    private static async Task<IResult> UpdateCartItemAsync(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. 必須已有 Cart Cookie
        // ======================================

        if (!TryGetCartKey(
                httpContext,
                out var cartKey))
        {
            return Results.NotFound(
                new
                {
                    message = "尚未建立購物車。"
                });
        }


        // ======================================
        // 2. 找 CartItem + Product
        // ======================================

        /*
            Include(item => item.Product)

            這就是這一步要練的：

            Navigation Property。

            CartItem
              ↓ ProductId FK
            Product

            EF Core 會把 Product 一起讀進來。
        */
        var cartItem =
            await db.CartItems
                .Include(item =>
                    item.Product)

                .FirstOrDefaultAsync(
                    item =>
                        item.CartKey == cartKey &&
                        item.ProductId == productId,
                    cancellationToken);


        if (cartItem is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"購物車中沒有 ProductId = {productId} 的商品。"
                });
        }


        // ======================================
        // 3. 檢查庫存
        // ======================================

        if (request.Quantity >
            cartItem.Product.Stock)
        {
            return Results.BadRequest(
                new
                {
                    message =
                        $"商品庫存不足，目前庫存為 {cartItem.Product.Stock}。"
                });
        }


        // ======================================
        // 4. 修改 tracked Entity
        // ======================================

        cartItem.Quantity =
            request.Quantity;

        cartItem.UpdatedAt =
            DateTime.UtcNow;


        // ======================================
        // 5. UPDATE SQLite
        // ======================================

        await db.SaveChangesAsync(
            cancellationToken);


        // ======================================
        // 6. 回傳更新後購物車
        // ======================================

        var cart =
            await BuildCartResponseAsync(
                db,
                cartKey,
                cancellationToken);

        return Results.Ok(cart);
    }


    // ==========================================
    // DELETE /api/cart/items/{productId}
    // ==========================================

    private static async Task<IResult> RemoveCartItemAsync(
        int productId,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryGetCartKey(
                httpContext,
                out var cartKey))
        {
            return Results.NotFound(
                new
                {
                    message = "尚未建立購物車。"
                });
        }


        var cartItem =
            await db.CartItems
                .FirstOrDefaultAsync(
                    item =>
                        item.CartKey == cartKey &&
                        item.ProductId == productId,
                    cancellationToken);


        if (cartItem is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"購物車中沒有 ProductId = {productId} 的商品。"
                });
        }


        /*
            Remove：

            Entity state
            → Deleted
        */
        db.CartItems.Remove(cartItem);


        /*
            SaveChangesAsync()
            才真正執行 DELETE。
        */
        await db.SaveChangesAsync(
            cancellationToken);


        /*
            回傳移除後最新購物車，
            前端之後可以直接更新數量 / 金額。
        */
        var cart =
            await BuildCartResponseAsync(
                db,
                cartKey,
                cancellationToken);

        return Results.Ok(cart);
    }


    // ==========================================
    // DELETE /api/cart
    // ==========================================

    /// <summary>
    /// 清空整台瀏覽器的購物車。
    /// </summary>
    private static async Task<IResult> ClearCartAsync(
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        /*
            沒有 Cart Cookie 本身就等於空購物車。

            DELETE 可以視為 idempotent：

            清空一個已經空的購物車，
            結果仍然是空購物車。
        */
        if (!TryGetCartKey(
                httpContext,
                out var cartKey))
        {
            return Results.Ok(
                CreateEmptyCartResponse());
        }


        /*
            找這個 CartKey 所有 CartItem。
        */
        var cartItems =
            await db.CartItems
                .Where(item =>
                    item.CartKey == cartKey)

                .ToListAsync(
                    cancellationToken);


        if (cartItems.Count > 0)
        {
            /*
                一次標記全部為 Deleted。
            */
            db.CartItems.RemoveRange(
                cartItems);


            await db.SaveChangesAsync(
                cancellationToken);
        }


        /*
            我們目前不刪 Cookie。

            Cookie 可以繼續代表同一個匿名購物車，
            只是目前裡面沒有商品。

            下次再加入商品時可以繼續使用同一 CartKey。
        */
        return Results.Ok(
            CreateEmptyCartResponse());
    }


    // ==========================================
    // Cookie helpers
    // ==========================================

    /// <summary>
    /// 嘗試從 Request Cookie 取得 CartKey。
    /// </summary>
    private static bool TryGetCartKey(
        HttpContext httpContext,
        out string cartKey)
    {
        /*
            Request.Cookies：

            Browser
                ↓
            Cookie request header
                ↓
            ASP.NET Core
                ↓
            Request.Cookies
        */
        if (httpContext.Request.Cookies.TryGetValue(
                CartCookieName,
                out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            cartKey = value;

            return true;
        }


        cartKey = string.Empty;

        return false;
    }


    /// <summary>
    /// 有 Cookie 就沿用。
    ///
    /// 沒有 Cookie：
    /// 1. 建立新 CartKey
    /// 2. 寫入 Response Cookie
    /// 3. 回傳 CartKey
    /// </summary>
    private static string GetOrCreateCartKey(
        HttpContext httpContext)
    {
        if (TryGetCartKey(
                httpContext,
                out var existingCartKey))
        {
            return existingCartKey;
        }


        /*
            Guid：

            例如：

            f31ca7f765bd42ef85a075ffafbe1a61

            "N" 格式會移除 '-'。
        */
        var newCartKey =
            Guid.NewGuid()
                .ToString("N");


        var cookieOptions =
            new CookieOptions
            {
                /*
                    JavaScript：

                    document.cookie

                    無法讀取這顆 Cookie。

                    Step 7 的 JS 不需要知道 CartKey，
                    Browser 自己會隨 request 帶 Cookie。
                */
                HttpOnly = true,


                /*
                    SameSite=Lax

                    提供基本跨站 request 限制。
                */
                SameSite =
                    SameSiteMode.Lax,


                /*
                    即使未來網站有 Cookie consent，
                    購物車 Cookie 可視為網站核心功能。
                */
                IsEssential = true,


                /*
                    目前本機開發使用 HTTP：

                    Request.IsHttps = false
                    → Secure = false

                    未來 HTTPS：
                    → Secure = true
                */
                Secure =
                    httpContext.Request.IsHttps,


                /*
                    Cookie 30 天後過期。
                */
                Expires =
                    DateTimeOffset.UtcNow
                        .AddDays(30),


                /*
                    整個網站都可以使用這顆 Cookie。
                */
                Path = "/"
            };


        /*
            Response.Cookies.Append：

            Server
                ↓
            Set-Cookie response header
                ↓
            Browser 保存 Cookie
        */
        httpContext.Response.Cookies.Append(
            CartCookieName,
            newCartKey,
            cookieOptions);


        return newCartKey;
    }


    // ==========================================
    // Response helper
    // ==========================================

    /// <summary>
    /// 根據 CartKey 建立 API Response。
    /// </summary>
    private static async Task<CartResponse>
        BuildCartResponseAsync(
            AppDbContext db,
            string cartKey,
            CancellationToken cancellationToken)
    {
        /*
            Include(Product)

            SQL 概念接近：

            CartItems
                JOIN
            Products

            EF Core 透過 Navigation Property
            把 Product 填到：

            cartItem.Product
        */
        var cartItems =
            await db.CartItems
                .AsNoTracking()

                .Include(item =>
                    item.Product)

                .Where(item =>
                    item.CartKey == cartKey)

                .OrderBy(item =>
                    item.Id)

                .ToListAsync(
                    cancellationToken);


        /*
            Entity
              ↓
            API DTO
        */
        var items =
            cartItems
                .Select(item =>
                    new CartItemDto(
                        ProductId:
                            item.ProductId,

                        Title:
                            item.Product.Title,

                        Price:
                            item.Product.Price,

                        Quantity:
                            item.Quantity,

                        LineTotal:
                            item.Product.Price *
                            item.Quantity,

                        ThumbnailUrl:
                            item.Product.ThumbnailUrl
                    ))

                .ToList();


        /*
            例如：

            商品 A × 2
            商品 B × 3

            TotalQuantity
            = 5
        */
        var totalQuantity =
            items.Sum(item =>
                item.Quantity);


        /*
            LineTotal 加總。
        */
        var totalAmount =
            items.Sum(item =>
                item.LineTotal);


        return new CartResponse(
            Items: items,
            TotalQuantity: totalQuantity,
            TotalAmount: totalAmount);
    }


    /// <summary>
    /// 建立一致格式的空購物車。
    /// </summary>
    private static CartResponse
        CreateEmptyCartResponse()
    {
        return new CartResponse(
            Items: [],
            TotalQuantity: 0,
            TotalAmount: 0m);
    }
}
