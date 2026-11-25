
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Cart API Integration Tests。
///
/// 主要驗證：
///
/// 1. 購物車 CRUD 操作
/// 2. 相同商品數量合併
/// 3. 商品小計與購物車總金額
/// 4. 商品庫存限制
/// 5. 不存在商品的錯誤處理
///
/// 每個 Test 都使用獨立的 Test Host
/// 與 SQLite In-Memory Database。
/// </summary>
public class CartApiTests
{

    // ==========================================
    // Cookie Helpers
    // ==========================================

    // 正式 API 對外使用的 Cookie 名稱。
    private const string CartCookieName =
        "razor_commerce_cart";


    /// <summary>
    /// 從 HTTP Response Header 取得購物車 Cookie。
    ///
    /// 使用 ASP.NET Core 提供的 Header Parser，
    /// 不需要自行使用 Split() 解析 Cookie 字串。
    /// </summary>
    private static SetCookieHeaderValue GetCartCookie(
        HttpResponseMessage response)
    {
        // 確認 Response 包含 Set-Cookie Header。
        Assert.True(
            response.Headers.TryGetValues(
                "Set-Cookie",
                out var headers
            )
        );

        // 將 Header 字串解析成 SetCookieHeaderValue 物件。
        var cookies = headers!
            .Select(header =>
                SetCookieHeaderValue.Parse(header));

        // 確認購物車 Cookie 剛好只有一個，
        // 並回傳該 Cookie 物件。
        //
        // 不假設 Response 只能包含購物車 Cookie，
        // 因為未來可能還會有其他用途的 Cookie。
        return Assert.Single(
            cookies,
            cookie => cookie.Name.Value == CartCookieName
        );
    }


    /// <summary>
    /// 確認 Response 沒有發放購物車 Cookie。
    ///
    /// 允許其他用途的 Cookie 存在。
    /// </summary>
    private static void AssertNoCartCookie(
        HttpResponseMessage response)
    {
        // 完全沒有 Set-Cookie Header，
        // 代表沒有發放任何 Cookie。
        if (!response.Headers.TryGetValues(
                "Set-Cookie",
                out var headers))
        {
            return;
        }

        // 如果有其他 Set-Cookie Header，
        // 確認其中沒有購物車 Cookie。
        Assert.DoesNotContain(
            headers,
            header => SetCookieHeaderValue
                .Parse(header)
                .Name.Value == CartCookieName
        );
    }


    // ==========================================
    // Test Seed Data
    // ==========================================

    /// <summary>
    /// 建立購物車測試所需的兩筆商品。
    ///
    /// 固定商品 Id、價格與庫存，
    /// 讓測試結果可以事先預測。
    /// </summary>
    private static async Task SeedProductsAsync(
        CustomWebApplicationFactory factory)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        db.Products.AddRange(
            new Product
            {
                Id = 1,
                Title = "Mechanical Keyboard",
                Description = "Test keyboard",
                Price = 1500m,
                Stock = 10,
                Brand = "Test Brand",
                Category = "electronics"
            },
            new Product
            {
                Id = 2,
                Title = "Wireless Mouse",
                Description = "Test mouse",
                Price = 800m,
                Stock = 20,
                Brand = "Test Brand",
                Category = "electronics"
            }
        );

        await db.SaveChangesAsync();
    }


    // ==========================================
    // HTTP Helpers
    // ==========================================

    /// <summary>
    /// 驗證 HTTP 200，
    /// 並將 JSON Response 反序列化成 CartResponse。
    ///
    /// GET、POST、PUT、DELETE 成功時，
    /// 都會回傳相同的 CartResponse 格式。
    /// </summary>
    private static async Task<CartResponse>
        ReadCartResponseAsync(
            HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        var cart = await response.Content
            .ReadFromJsonAsync<CartResponse>();

        Assert.NotNull(cart);

        return cart!;
    }


    /// <summary>
    /// 發送新增商品的 POST Request。
    ///
    /// 封裝多個 Test 都會使用的新增商品操作。
    ///
    /// 此 Helper 只適用於預期成功的 Request。
    /// 如果要測試 HTTP 400 或 404，
    /// 則直接使用 PostAsJsonAsync()。
    /// </summary>
    private static async Task<CartResponse>
        AddItemAsync(
            HttpClient client,
            int productId,
            int quantity)
    {
        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            );

        return await ReadCartResponseAsync(response);
    }


    /// <summary>
    /// 取得目前購物車的內容。
    ///
    /// 使用同一個 HttpClient，
    /// 可以繼續存取先前建立的購物車。
    /// </summary>
    private static async Task<CartResponse>
        GetCartAsync(HttpClient client)
    {
        using var response =
            await client.GetAsync("/api/cart");

        return await ReadCartResponseAsync(response);
    }


    /// <summary>
    /// 驗證空購物車的標準回應格式。
    /// </summary>
    private static void AssertEmptyCart(
        CartResponse cart)
    {
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalQuantity);
        Assert.Equal(0m, cart.TotalAmount);
    }


    // ==========================================
    // Test 1：Empty Cart
    // ==========================================

    [Fact]
    public async Task GetCart_ShouldReturnEmptyCart_WhenCartDoesNotExist()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // 此情境不需要 Seed 商品。
        // 因為新的 Client 尚未建立任何購物車。

        // Act
        var cart = await GetCartAsync(client);

        // Assert
        AssertEmptyCart(cart);
    }


    // ==========================================
    // Test 2：Add Item
    // ==========================================

    [Fact]
    public async Task AddCartItem_ShouldReturnUpdatedCart_WhenProductExists()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        // 加入兩個 Mechanical Keyboard。
        var cart = await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        // Assert

        // Assert.Single()：
        // 確認集合只有一筆資料，
        // 並直接回傳該元素。
        var item = Assert.Single(cart.Items);

        Assert.Equal(1, item.ProductId);
        Assert.Equal("Mechanical Keyboard", item.Title);
        Assert.Equal(1500m, item.Price);
        Assert.Equal(2, item.Quantity);

        // 商品小計 = 單價 × 數量。
        Assert.Equal(3000m, item.LineTotal);

        // 驗證整個購物車的合計。
        Assert.Equal(2, cart.TotalQuantity);
        Assert.Equal(3000m, cart.TotalAmount);

        // 再發送 GET Request，
        // 確認購物車狀態可以被重新取得，
        // 而不是只有 POST Response 正確。
        var savedCart = await GetCartAsync(client);

        var savedItem =
            Assert.Single(savedCart.Items);

        Assert.Equal(1, savedItem.ProductId);
        Assert.Equal(2, savedItem.Quantity);
        Assert.Equal(3000m, savedCart.TotalAmount);
    }


    // ==========================================
    // Test 3：Add Same Item Twice
    // ==========================================

    [Fact]
    public async Task AddCartItem_ShouldMergeQuantity_WhenProductAlreadyExists()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // 第一次加入兩個商品。
        await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        // Act
        // 第二次加入相同商品三個。
        var cart = await AddItemAsync(
            client,
            productId: 1,
            quantity: 3
        );

        // Assert

        // 相同商品應合併數量，
        // 而不是建立兩個購物車項目。
        var item = Assert.Single(cart.Items);

        Assert.Equal(1, item.ProductId);

        // 原本 2 個，再加入 3 個。
        Assert.Equal(5, item.Quantity);

        Assert.Equal(7500m, item.LineTotal);
        Assert.Equal(5, cart.TotalQuantity);
        Assert.Equal(7500m, cart.TotalAmount);
    }


    // ==========================================
    // Test 4：Update Quantity
    // ==========================================

    [Fact]
    public async Task UpdateCartItem_ShouldReplaceQuantity_WhenItemExists()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // 原本購物車有兩個商品。
        await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        // Act
        // PUT 是設定新的商品數量，
        // 不是在原本數量上繼續增加。
        using var response =
            await client.PutAsJsonAsync(
                "/api/cart/items/1",
                new UpdateCartItemRequest
                {
                    Quantity = 4
                }
            );

        var cart =
            await ReadCartResponseAsync(response);

        // Assert
        var item = Assert.Single(cart.Items);

        Assert.Equal(1, item.ProductId);

        // 正確結果是 4，而不是 2 + 4 = 6。
        Assert.Equal(4, item.Quantity);

        Assert.Equal(6000m, item.LineTotal);
        Assert.Equal(4, cart.TotalQuantity);
        Assert.Equal(6000m, cart.TotalAmount);
    }


    // ==========================================
    // Test 5：Remove Item
    // ==========================================

    [Fact]
    public async Task RemoveCartItem_ShouldRemoveOnlySpecifiedProduct()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // 加入兩個鍵盤。
        await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        // 加入三個滑鼠。
        var initialCart = await AddItemAsync(
            client,
            productId: 2,
            quantity: 3
        );

        // 確認測試前置條件正確。
        Assert.Equal(2, initialCart.Items.Count);
        Assert.Equal(5, initialCart.TotalQuantity);
        Assert.Equal(5400m, initialCart.TotalAmount);

        // Act
        // 只移除 ProductId = 1。
        using var response =
            await client.DeleteAsync(
                "/api/cart/items/1"
            );

        var cart =
            await ReadCartResponseAsync(response);

        // Assert

        // 購物車應只剩下 Wireless Mouse。
        var remainingItem =
            Assert.Single(cart.Items);

        Assert.Equal(2, remainingItem.ProductId);
        Assert.Equal("Wireless Mouse", remainingItem.Title);
        Assert.Equal(3, remainingItem.Quantity);
        Assert.Equal(2400m, remainingItem.LineTotal);

        Assert.Equal(3, cart.TotalQuantity);
        Assert.Equal(2400m, cart.TotalAmount);
    }


    // ==========================================
    // Test 6：Clear Cart
    // ==========================================

    [Fact]
    public async Task ClearCart_ShouldRemoveAllItems()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        var initialCart = await AddItemAsync(
            client,
            productId: 2,
            quantity: 3
        );

        // 確保清空前購物車確實有商品。
        Assert.Equal(2, initialCart.Items.Count);

        // Act
        using var response =
            await client.DeleteAsync("/api/cart");

        var cart =
            await ReadCartResponseAsync(response);

        // Assert

        // 確認 DELETE Response 是空購物車。
        AssertEmptyCart(cart);

        // 再執行一次 GET，
        // 確認清空後的狀態確實維持。
        var savedCart = await GetCartAsync(client);

        AssertEmptyCart(savedCart);
    }


    // ==========================================
    // Test 7：Unknown Product
    // ==========================================

    [Fact]
    public async Task AddCartItem_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        // ProductId = 999999 符合 DTO Validation，
        // 但資料庫裡沒有這筆商品。
        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 999999,
                    Quantity = 1
                }
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        // 新增失敗不應產生購物車商品。
        var cart = await GetCartAsync(client);

        AssertEmptyCart(cart);
    }


    // ==========================================
    // Test 8：Insufficient Stock
    // ==========================================

    [Fact]
    public async Task AddCartItem_ShouldReturnBadRequest_WhenQuantityExceedsStock()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Mechanical Keyboard 的庫存為 10。

        // Act
        // 嘗試加入 11 個商品。
        //
        // Quantity = 11 符合 DTO 的 1~99 限制，
        // 但超過資料庫裡的商品庫存。
        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 11
                }
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        // 操作失敗後，
        // 購物車仍應維持空的狀態。
        var cart = await GetCartAsync(client);

        AssertEmptyCart(cart);
    }


    // ==========================================
    // Test 9：Combined Quantity Exceeds Stock
    // ==========================================

    [Fact]
    public async Task AddCartItem_ShouldPreserveQuantity_WhenCombinedQuantityExceedsStock()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // 商品庫存為 10。
        //
        // 第一次成功加入 8 個。
        var initialCart = await AddItemAsync(
            client,
            productId: 1,
            quantity: 8
        );

        Assert.Equal(8, initialCart.TotalQuantity);

        // Act
        // 第二次加入 3 個。
        //
        // 單次數量 3 沒有超過庫存，
        // 但合併後 8 + 3 = 11，超過庫存。
        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 3
                }
            );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        // 重新取得購物車。
        var cart = await GetCartAsync(client);

        var item = Assert.Single(cart.Items);

        Assert.Equal(1, item.ProductId);

        // 操作失敗不應影響原本的數量。
        Assert.Equal(8, item.Quantity);

        Assert.Equal(12000m, item.LineTotal);
        Assert.Equal(8, cart.TotalQuantity);
        Assert.Equal(12000m, cart.TotalAmount);
    }


    // ==========================================
    // Test 10：GET Without Cookie
    // ==========================================

    /// <summary>
    /// 尚未建立購物車的 Client 執行 GET 時，
    /// 應回傳空購物車，而不是發放新的 Cart Cookie。
    /// </summary>
    [Fact]
    public async Task GetCart_ShouldNotIssueCookie_WhenCartDoesNotExist()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // 不需要 Seed Data，
        // 因為這次只測試空購物車。

        // Act
        using var response =
            await client.GetAsync("/api/cart");

        // Assert

        // 沿用 Step 16-13 的 HTTP Response Helper。
        var cart = await ReadCartResponseAsync(response);

        AssertEmptyCart(cart);

        // 讀取空購物車不應建立新的 Cart Cookie。
        AssertNoCartCookie(response);
    }

    // ==========================================
    // Test 11：Issue Cart Cookie
    // ==========================================

    /// <summary>
    /// 第一次加入商品時，Server 應建立 Cart Cookie。
    ///
    /// 同時驗證 HTTP 和 HTTPS Request
    /// 對 Cookie Secure 屬性的影響。
    /// </summary>
    [Theory]
    [InlineData("http://localhost", false)]
    [InlineData("https://localhost", true)]
    public async Task AddCartItem_ShouldIssueCookie_WhenCartDoesNotExist(
        string baseAddress,
        bool expectedSecure)
    {
        // ========================================
        // Arrange
        // ========================================

        using var factory =
            new CustomWebApplicationFactory();

        // WebApplicationFactoryClientOptions
        // 可以自訂測試 HttpClient 的行為。
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri(baseAddress),

                // 啟用自動 Cookie Handling。
                // 這個選項預設就是 true，
                // 此處明確設定是為了方便學習。
                HandleCookies = true
            }
        );

        await SeedProductsAsync(factory);


        // ========================================
        // Act
        // ========================================

        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 2
                }
            );


        // ========================================
        // Assert：HTTP Response
        // ========================================

        var cart = await ReadCartResponseAsync(response);

        Assert.Equal(2, cart.TotalQuantity);


        // ========================================
        // Assert：Cookie Header
        // ========================================

        var cookie = GetCartCookie(response);

        // 確認 Cookie 名稱。
        Assert.Equal(
            CartCookieName,
            cookie.Name.Value
        );

        // Guid.TryParseExact()：
        //
        // 嘗試將字串解析成指定格式的 Guid。
        //
        // N 格式：
        // 32 個十六進位字元，不包含連字號。
        Assert.True(
            Guid.TryParseExact(
                cookie.Value.Value,
                "N",
                out var cartKey
            )
        );

        // 確認不是全部為 0 的 Guid。
        Assert.NotEqual(Guid.Empty, cartKey);


        // ========================================
        // Assert：Cookie Attributes
        // ========================================

        // JavaScript 不應直接讀取 Cookie。
        Assert.True(cookie.HttpOnly);

        // 確認 SameSite=Lax。
        Assert.Equal(
            SameSiteMode.Lax,
            cookie.SameSite
        );

        // HTTP  預期 Secure = false。
        // HTTPS 預期 Secure = true。
        Assert.Equal(
            expectedSecure,
            cookie.Secure
        );

        // Cookie 適用於整個網站。
        Assert.Equal(
            "/",
            cookie.Path.Value
        );


        // ========================================
        // Assert：Cookie Expiration
        // ========================================

        // 確認 Cookie 有設定過期時間，
        // 而不是只有瀏覽器 Session 期間有效。
        Assert.NotNull(cookie.Expires);

        var now = DateTimeOffset.UtcNow;

        // 正式程式設定 30 天。
        //
        // 使用合理的時間容許範圍，
        // 避免直接比較 DateTimeOffset 導致測試不穩定。
        Assert.InRange(
            cookie.Expires.GetValueOrDefault(),
            now.AddDays(29),
            now.AddDays(31)
        );
    }


    // ==========================================
    // Test 12：Reuse Existing Cart Cookie
    // ==========================================

    /// <summary>
    /// 同一個 HttpClient 再次加入商品時，
    /// 應沿用既有 Cookie，
    /// 而不是建立新的購物車識別碼。
    /// </summary>
    [Fact]
    public async Task AddCartItem_ShouldReuseCookie_WhenCartAlreadyExists()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // 第一次加入商品。
        //
        // Server 應建立 CartKey，
        // HttpClient 會自動保存 Cookie。
        var initialCart = await AddItemAsync(
            client,
            productId: 1,
            quantity: 2
        );

        Assert.Equal(2, initialCart.TotalQuantity);


        // Act

        // 再次使用同一個 HttpClient。
        //
        // Cookie Handling 會自動將之前收到的
        // Cart Cookie 加入 Request Header。
        using var response =
            await client.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 3
                }
            );


        // Assert

        var cart = await ReadCartResponseAsync(response);

        var item = Assert.Single(cart.Items);

        Assert.Equal(1, item.ProductId);

        // 如果 Cookie 沒有正確傳遞，
        // Server 可能建立新的購物車，
        // 數量就會變成 3，而不是 5。
        Assert.Equal(5, item.Quantity);

        // 確認這次 Response 沒有重新發放 Cart Cookie。
        AssertNoCartCookie(response);

        // 再次透過 GET 確認購物車狀態。
        var savedCart = await GetCartAsync(client);

        Assert.Equal(5, savedCart.TotalQuantity);
        Assert.Equal(7500m, savedCart.TotalAmount);
    }


    // ==========================================
    // Test 13：Cart Isolation
    // ==========================================

    /// <summary>
    /// 不同 HttpClient 應具有獨立的購物車。
    ///
    /// 即使使用相同 Test Host、
    /// 相同 SQLite Database、相同 ProductId，
    /// 也不能讀取或修改其他 Client 的購物車。
    /// </summary>
    [Fact]
    public async Task Cart_ShouldRemainIsolated_WhenClientsUseDifferentCookies()
    {
        // ========================================
        // Arrange
        // ========================================

        // 注意：只建立一個 Factory。
        //
        // 兩個 Client 必須共用同一個資料庫，
        // 才能有效測試 CartKey 隔離。
        using var factory =
            new CustomWebApplicationFactory();

        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        await SeedProductsAsync(factory);


        // ========================================
        // Act 1：Client A 加入商品
        // ========================================

        using var responseA =
            await clientA.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 2
                }
            );

        var initialCartA =
            await ReadCartResponseAsync(responseA);

        var cookieA = GetCartCookie(responseA);

        Assert.Equal(2, initialCartA.TotalQuantity);


        // ========================================
        // Act 2：Client B 讀取購物車
        // ========================================

        // Client B 尚未加入任何商品。
        //
        // 即使資料庫中已經有 Client A 的商品，
        // Client B 仍然應該取得空購物車。
        var emptyCartB = await GetCartAsync(clientB);

        AssertEmptyCart(emptyCartB);


        // ========================================
        // Act 3：Client B 加入相同商品
        // ========================================

        using var responseB =
            await clientB.PostAsJsonAsync(
                "/api/cart/items",
                new AddCartItemRequest
                {
                    ProductId = 1,
                    Quantity = 3
                }
            );

        var initialCartB =
            await ReadCartResponseAsync(responseB);

        var cookieB = GetCartCookie(responseB);

        Assert.Equal(3, initialCartB.TotalQuantity);

        // 確認兩個 Client 取得不同的 CartKey。
        Assert.NotEqual(
            cookieA.Value.Value,
            cookieB.Value.Value
        );


        // ========================================
        // Assert 1：購物車資料隔離
        // ========================================

        var cartA = await GetCartAsync(clientA);
        var cartB = await GetCartAsync(clientB);

        var itemA = Assert.Single(cartA.Items);
        var itemB = Assert.Single(cartB.Items);

        // 兩個購物車包含同一個商品。
        Assert.Equal(1, itemA.ProductId);
        Assert.Equal(1, itemB.ProductId);

        // 但數量必須彼此獨立。
        Assert.Equal(2, itemA.Quantity);
        Assert.Equal(3, itemB.Quantity);

        // 金額也必須各自計算。
        Assert.Equal(3000m, cartA.TotalAmount);
        Assert.Equal(4500m, cartB.TotalAmount);


        // ========================================
        // Act 4：Client A 清空購物車
        // ========================================

        using var clearResponse =
            await clientA.DeleteAsync("/api/cart");

        var clearedCartA =
            await ReadCartResponseAsync(clearResponse);

        AssertEmptyCart(clearedCartA);


        // ========================================
        // Assert 2：刪除操作不能影響其他 Client
        // ========================================

        var finalCartA = await GetCartAsync(clientA);
        var finalCartB = await GetCartAsync(clientB);

        // Client A 已經清空。
        AssertEmptyCart(finalCartA);

        // Client B 的資料必須完全保留。
        var remainingItem = Assert.Single(finalCartB.Items);

        Assert.Equal(1, remainingItem.ProductId);
        Assert.Equal(3, remainingItem.Quantity);

        Assert.Equal(3, finalCartB.TotalQuantity);
        Assert.Equal(4500m, finalCartB.TotalAmount);
    }
}
