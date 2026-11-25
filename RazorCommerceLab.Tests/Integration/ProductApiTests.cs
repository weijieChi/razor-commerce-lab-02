
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Product GET API Integration Tests。
///
/// 驗證 HTTP Request 經過：
///
/// HttpClient
/// → TestServer
/// → ProductEndpoints
/// → EF Core
/// → SQLite
/// → JSON Response
///
/// 每個 Test 都建立獨立的 Test Host 與資料庫。
/// </summary>
public class ProductApiTests
{
    // ==========================================
    // Test Seed Data
    // ==========================================

    /// <summary>
    /// 在指定 Factory 的測試資料庫中，
    /// 建立 30 筆固定商品。
    ///
    /// 不使用正式資料庫，
    /// 也不呼叫外部 DummyJSON API。
    /// </summary>
    private static async Task SeedProductsAsync(
        CustomWebApplicationFactory factory)
    {
        // AppDbContext 是 Scoped Service。
        // 因此在 HTTP Request 之外，
        // 必須自行建立 DI Scope。
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        // 使用固定的 Id，
        // 讓分頁測試可以準確驗證商品順序。
        //
        // 這裡是測試資料，
        // 正式 API 新增商品仍然由 DB 產生 Id。
        var products = new List<Product>
        {
            // Title 符合 Keyboard 搜尋條件。
            new Product
            {
                Id = 1,
                Title = "Mechanical Keyboard",
                Description = "Mechanical switches",
                Price = 1500m,
                Stock = 10,
                Brand = "Test Brand",
                Category = "electronics"
            },

            // Description 符合 Keyboard 搜尋條件。
            new Product
            {
                Id = 2,
                Title = "Wireless Mouse",
                Description = "Compatible with Keyboard",
                Price = 800m,
                Stock = 20,
                Brand = "Test Brand",
                Category = "electronics"
            },

            // Brand 符合 Keyboard 搜尋條件。
            new Product
            {
                Id = 3,
                Title = "Laptop Stand",
                Description = "Adjustable laptop stand",
                Price = 600m,
                Stock = 15,
                Brand = "KeyboardWorks",
                Category = "electronics"
            },

            new Product
            {
                Id = 4,
                Title = "Facial Cleanser",
                Price = 300m,
                Stock = 20,
                Category = "beauty"
            },

            new Product
            {
                Id = 5,
                Title = "Hand Cream",
                Price = 200m,
                Stock = 30,
                Category = "beauty"
            },

            new Product
            {
                Id = 6,
                Title = "Face Mask",
                Price = 100m,
                Stock = 50,
                Category = "beauty"
            }
        };

        // 補足剩下的 24 筆商品。
        //
        // 目的不是模擬真實商品，
        // 而是提供足夠資料測試 Pagination。
        for (int id = 7; id <= 30; id++)
        {
            products.Add(new Product
            {
                Id = id,
                Title = $"Test Product {id:D2}",
                Description = "Test product description",
                Price = 100m,
                Stock = 10,
                Brand = "Test Brand",
                Category = "furniture"
            });
        }

        // 將 30 筆商品加入 Change Tracker。
        db.Products.AddRange(products);

        // 實際寫入 SQLite In-Memory Database。
        await db.SaveChangesAsync();

        // 方法結束後 Scope 會被 Dispose。
        //
        // 但 Factory 的 SQLite Singleton Connection
        // 仍然保持開啟，所以資料不會消失。
    }


    // ==========================================
    // 共用 HTTP Response Helper
    // ==========================================

    /// <summary>
    /// 發送商品列表 GET Request，
    /// 並將 JSON Response 轉成 PagedResult。
    ///
    /// 集中處理所有列表測試都需要的：
    /// 1. HTTP 200 檢查
    /// 2. JSON 反序列化
    /// 3. Null 檢查
    /// </summary>
    private static async Task<PagedResult<ProductDto>>
        GetProductListAsync(
            HttpClient client,
            string requestUri)
    {
        using var response =
            await client.GetAsync(requestUri);

        // 確認 API 回傳 HTTP 200。
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        // 將 JSON Response 反序列化成
        // 正式專案已經定義好的 DTO。
        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<ProductDto>>();

        // JSON Response 不應為 null。
        Assert.NotNull(result);

        return result!;
    }


    // ==========================================
    // Test 1：商品列表
    // ==========================================

    /// <summary>
    /// 不傳入任何 Query String，
    /// 應取得預設分頁的商品列表。
    /// </summary>
    [Fact]
    public async Task GetProducts_ShouldReturnDefaultPage()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        var result = await GetProductListAsync(
            client,
            "/api/products"
        );

        // Assert

        // 預設頁碼為 1。
        Assert.Equal(1, result.Page);

        // 預設每頁顯示 12 筆。
        Assert.Equal(12, result.PageSize);

        // DB 共有 30 筆商品。
        Assert.Equal(30, result.TotalItems);

        // 30 / 12 無條件進位，共 3 頁。
        Assert.Equal(3, result.TotalPages);

        // 第一頁應回傳 12 筆商品。
        Assert.Equal(12, result.Items.Count);

        // 驗證預設排序為 Id ASC。
        Assert.Equal(
            Enumerable.Range(1, 12),
            result.Items.Select(product => product.Id)
        );
    }


    // ==========================================
    // Test 2：Pagination
    // ==========================================

    /// <summary>
    /// 查詢第二頁，每頁 10 筆商品，
    /// 應回傳 Id 11 到 20。
    /// </summary>
    [Fact]
    public async Task GetProducts_ShouldReturnRequestedPage()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        var result = await GetProductListAsync(
            client,
            "/api/products?page=2&pageSize=10"
        );

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(30, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count);

        // 不只檢查回傳數量，
        // 還要檢查是否真的跳過第一頁。
        //
        // Enumerable.Range(11, 10)
        // 會產生 11, 12, ... 20。
        Assert.Equal(
            Enumerable.Range(11, 10),
            result.Items.Select(product => product.Id)
        );
    }


    // ==========================================
    // Test 3：Keyword Search
    // ==========================================

    /// <summary>
    /// Keyword 應能搜尋 Title、
    /// Description 與 Brand。
    /// </summary>
    [Fact]
    public async Task GetProducts_ShouldFilterByKeyword()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        var result = await GetProductListAsync(
            client,
            "/api/products?keyword=Keyboard"
        );

        // Assert

        // 預期只有商品 1、2、3 符合。
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.TotalPages);

        // 驗證精確的商品 Id，
        // 避免 API 回傳三筆錯誤商品仍通過。
        Assert.Equal(
            new[] { 1, 2, 3 },
            result.Items.Select(product => product.Id)
        );
    }


    // ==========================================
    // Test 4：Category Filter
    // ==========================================

    /// <summary>
    /// 指定 beauty 分類，
    /// 應只取得該分類的商品。
    /// </summary>
    [Fact]
    public async Task GetProducts_ShouldFilterByCategory()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        //
        // 這裡明確傳入 page 和 pageSize，
        // 專注驗證 Category Filter。
        //
        // 省略分頁參數的特定情境，
        // 留到 Step 16-12 Regression Test。
        var result = await GetProductListAsync(
            client,
            "/api/products?category=beauty&page=1&pageSize=12"
        );

        // Assert
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.TotalPages);

        // Assert.All()：
        // 對集合中的每一個元素執行 Assertion。
        //
        // 任何一筆商品不是 beauty，
        // 整個 Test 就會失敗。
        Assert.All(
            result.Items,
            product => Assert.Equal(
                "beauty",
                product.Category
            )
        );

        // 驗證取得的是正確的三筆商品。
        Assert.Equal(
            new[] { 4, 5, 6 },
            result.Items.Select(product => product.Id)
        );
    }


    // ==========================================
    // Test 5：Get Product By Id
    // ==========================================

    /// <summary>
    /// 查詢存在的 Product Id，
    /// 應回傳 HTTP 200 與正確商品內容。
    /// </summary>
    [Fact]
    public async Task GetProductById_ShouldReturnProduct_WhenProductExists()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        using var response = await client.GetAsync(
            "/api/products/1"
        );

        // Assert：HTTP Status Code
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        // 單一商品 API 並沒有使用 PagedResult。
        //
        // 因此直接反序列化成 ProductDto。
        var product = await response.Content
            .ReadFromJsonAsync<ProductDto>();

        Assert.NotNull(product);

        // 驗證 API 回傳的商品內容。
        Assert.Equal(1, product!.Id);
        Assert.Equal("Mechanical Keyboard", product.Title);
        Assert.Equal("Mechanical switches", product.Description);
        Assert.Equal(1500m, product.Price);
        Assert.Equal(10, product.Stock);
        Assert.Equal("Test Brand", product.Brand);
        Assert.Equal("electronics", product.Category);
    }


    // ==========================================
    // Test 6：Product Not Found
    // ==========================================

    /// <summary>
    /// 查詢不存在的商品，
    /// 應回傳 HTTP 404 與錯誤訊息。
    /// </summary>
    [Fact]
    public async Task GetProductById_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        await SeedProductsAsync(factory);

        // Act
        //
        // Route 本身存在，
        // 但是 Product Id 不存在。
        using var response = await client.GetAsync(
            "/api/products/999999"
        );

        // Assert：HTTP 404
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        // 正式 API 在商品不存在時，
        // 會回傳包含 message 欄位的 JSON。
        //
        // 因為目前沒有專用的 Error DTO，
        // 所以先使用 JsonDocument 解析。
        var json = await response.Content
            .ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        // 取得 JSON 中的 message 屬性。
        var message = document.RootElement
            .GetProperty("message")
            .GetString();

        Assert.NotNull(message);

        // 驗證訊息包含查詢的商品 Id。
        // 不強制比對完整中文文字，
        // 避免單純修改文案就造成測試失敗。
        Assert.Contains("999999", message!);
    }


    // ==========================================
    // Test 7：Regression Test
    // ==========================================

    /// <summary>
    /// Regression Test：
    ///
    /// 防止 Minimal API 的 [AsParameters]
    /// Query Parameter Binding 問題再次發生。
    ///
    /// 當 Request 只提供 Category，
    /// 沒有提供 Page 和 PageSize 時，
    /// API 仍應正常使用預設分頁設定。
    /// </summary>
    [Fact]
    public async Task GetProducts_ShouldUseDefaultPagination_WhenOnlyCategoryIsProvided()
    {
        // ========================================
        // Arrange
        // ========================================

        // 建立獨立的 ASP.NET Core Test Host
        // 與 SQLite In-Memory Database。
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // 沿用 Step 16-11 的 30 筆測試商品。
        //
        // 其中 Id 4、5、6 屬於 beauty 分類。
        await SeedProductsAsync(factory);


        // ========================================
        // Act
        // ========================================

        // 刻意不提供 page 和 pageSize。
        //
        // 這是之前曾經發生 Bug 的 Request 情境。
        //
        // GetProductListAsync() 內部已經包含：
        // 1. HTTP 200 OK Assertion
        // 2. JSON Deserialization
        // 3. Response Null Check
        var result = await GetProductListAsync(
            client,
            "/api/products?category=beauty"
        );


        // ========================================
        // Assert
        // ========================================

        // 確認省略分頁參數時，
        // API 仍然正確使用預設值。
        Assert.Equal(1, result.Page);
        Assert.Equal(12, result.PageSize);

        // 確認分類篩選結果正確。
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(3, result.Items.Count);

        // 確認每筆商品都屬於 beauty 分類。
        Assert.All(
            result.Items,
            product => Assert.Equal(
                "beauty",
                product.Category
            )
        );

        // 確認回傳的是正確的商品，
        // 而不只是剛好有三筆資料。
        Assert.Equal(
            new[] { 4, 5, 6 },
            result.Items.Select(product => product.Id)
        );
    }

}
