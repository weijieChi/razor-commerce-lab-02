
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Minimal API Validation Integration Tests。
///
/// 主要驗證：
///
/// 1. DataAnnotations Validation Pipeline
/// 2. HTTP 400 Bad Request
/// 3. Validation Problem Details
/// 4. 不合法的 Request 不會寫入資料庫
/// 5. 不合法的更新不會破壞既有資料
///
/// 使用 Theory、InlineData 和 MemberData
/// 減少重複的測試程式碼。
/// </summary>
public class ValidationApiTests
{
    private const string AdminProductUrl =
        "/api/admin/products";

    private const string CartItemsUrl =
        "/api/cart/items";


    // ==========================================
    // Test Request Factory
    // ==========================================

    /// <summary>
    /// 建立一個預設合法的商品 Request DTO。
    ///
    /// 使用 Optional Parameters，
    /// 讓每個測試只需要修改想要驗證的欄位。
    ///
    /// 其他欄位維持合法狀態，
    /// 避免同時發生多個 Validation Error。
    /// </summary>
    private static AdminProductWriteRequest
        CreateProductRequest(
            string title = "Mechanical Keyboard",
            decimal price = 1500m,
            int stock = 10,
            decimal rating = 4.5m,
            decimal discountPercentage = 10m,
            string category = "electronics")
    {
        return new AdminProductWriteRequest
        {
            Title = title,
            Description = "Test keyboard",

            Price = price,
            Stock = stock,
            Rating = rating,
            DiscountPercentage = discountPercentage,

            Brand = "Test Brand",
            Category = category,

            ThumbnailUrl =
                "https://example.com/keyboard.jpg",

            IsFeatured = false
        };
    }


    /// <summary>
    /// 將商品 Request DTO 序列化成 JSON 字串。
    ///
    /// JsonSerializerOptions.Web 使用 Web 預設設定，
    /// 例如將 Property Name 轉換成 camelCase。
    ///
    /// 使用 JSON 字串作為 MemberData，
    /// 避免 xUnit 2 對自訂物件測試參數的
    /// 序列化與獨立探索限制。
    /// </summary>
    private static string SerializeRequest(
        AdminProductWriteRequest request)
    {
        return JsonSerializer.Serialize(
            request,
            JsonSerializerOptions.Web
        );
    }


    // ==========================================
    // MemberData
    // ==========================================

    /// <summary>
    /// 後台商品 API 的非法輸入測試資料。
    ///
    /// TheoryData 的三個欄位：
    ///
    /// 1. 預期發生錯誤的 Property Name
    /// 2. 預期錯誤訊息
    /// 3. JSON Request Body
    ///
    /// 每一筆資料只包含一個非法欄位。
    /// </summary>
    public static TheoryData<string, string, string>
        InvalidProductRequests =>
        new()
        {
            // Test Case 1：Required
            {
                nameof(AdminProductWriteRequest.Title),
                "title 為必填欄位。",
                SerializeRequest(
                    CreateProductRequest(title: "")
                )
            },

            // Test Case 2：StringLength
            {
                nameof(AdminProductWriteRequest.Title),
                "title 長度不可超過 120 個字元。",
                SerializeRequest(
                    CreateProductRequest(
                        title: new string('A', 121)
                    )
                )
            },

            // Test Case 3：Negative Price
            {
                nameof(AdminProductWriteRequest.Price),
                "price 必須介於 0 到 99999999。",
                SerializeRequest(
                    CreateProductRequest(price: -100m)
                )
            },

            // Test Case 4：Negative Stock
            {
                nameof(AdminProductWriteRequest.Stock),
                "stock 不可小於 0。",
                SerializeRequest(
                    CreateProductRequest(stock: -1)
                )
            },

            // Test Case 5：Rating Range
            {
                nameof(AdminProductWriteRequest.Rating),
                "rating 必須介於 0 到 5。",
                SerializeRequest(
                    CreateProductRequest(rating: 6m)
                )
            },

            // Test Case 6：Discount Range
            {
                nameof(AdminProductWriteRequest.DiscountPercentage),
                "discountPercentage 必須介於 0 到 100。",
                SerializeRequest(
                    CreateProductRequest(
                        discountPercentage: 101m
                    )
                )
            },

            // Test Case 7：Required Category
            {
                nameof(AdminProductWriteRequest.Category),
                "category 為必填欄位。",
                SerializeRequest(
                    CreateProductRequest(category: "")
                )
            }
        };


    // ==========================================
    // Validation Response Helper
    // ==========================================


    /// <summary>
    /// 驗證 Minimal API 回傳的 Validation Error。
    ///
    /// 配合目前專案的 JSON fallback response，
    /// 不強制要求 application/problem+json。
    /// </summary>
    private static async Task AssertValidationErrorAsync(
        HttpResponseMessage response,
        string expectedField,
        string expectedMessage)
    {
        // ========================================
        // HTTP Status Code
        // ========================================

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );


        // ========================================
        // Response Content-Type
        // ========================================

        // 目前專案實際回傳 application/json。
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType
        );


        // ========================================
        // Response Body
        // ========================================

        // 先取得原始 JSON 字串，
        // 方便測試失敗時進行除錯。
        var responseBody =
            await response.Content.ReadAsStringAsync();

        // 暫時輸出 Response Body，
        // 方便確認框架實際回傳的 JSON 結構。
        // Console.WriteLine(responseBody);


        // ========================================
        // JSON Deserialization
        // ========================================

        var problem =
            JsonSerializer.Deserialize<HttpValidationProblemDetails>(
                responseBody,
                JsonSerializerOptions.Web
            );

        Assert.NotNull(problem);


        // ========================================
        // Problem Details Status
        // ========================================

        // JSON Body 內的 Status 是 nullable int。
        //
        // 如果框架有提供 Status，
        // 就必須與 HTTP Status Code 一致。
        //
        // 如果沒有提供，仍以 HTTP Response
        // 的 Status Code 作為判斷依據。
        if (problem!.Status.HasValue)
        {
            Assert.Equal(
                400,
                problem.Status.Value
            );
        }


        // ========================================
        // Validation Error Field
        // ========================================

        // 每個測試只有一個非法欄位。
        var error = Assert.Single(problem.Errors);

        // 忽略可能存在的 Request 參數前綴。
        var actualField = error.Key
            .Split('.')
            .Last();

        Assert.Equal(
            expectedField,
            actualField,
            ignoreCase: true
        );


        // ========================================
        // Validation Error Message
        // ========================================

        Assert.Contains(
            expectedMessage,
            error.Value
        );
    }


    // ==========================================
    // Test 1：Admin POST Validation
    // ==========================================

    /// <summary>
    /// 驗證非法商品 Request 無法建立商品。
    ///
    /// MemberData 提供七組測試資料，
    /// 因此這個方法會執行七次。
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidProductRequests))]
    public async Task CreateProduct_ShouldRejectInvalidRequest(
        string expectedField,
        string expectedMessage,
        string requestJson)
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // MemberData 已經提供序列化後的 JSON。
        //
        // StringContent 可以直接將 JSON 字串
        // 作為 HTTP Request Body。
        using var content = new StringContent(
            requestJson,
            Encoding.UTF8,
            "application/json"
        );


        // Act
        using var response =
            await client.PostAsync(
                AdminProductUrl,
                content
            );


        // Assert：Validation Response
        await AssertValidationErrorAsync(
            response,
            expectedField,
            expectedMessage
        );


        // Assert：Database State

        // 建立新的 DI Scope 與 DbContext，
        // 直接確認資料庫沒有新增任何商品。
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Equal(
            0,
            await db.Products.CountAsync()
        );
    }


    // ==========================================
    // Test 2：Admin PUT Validation
    // ==========================================

    /// <summary>
    /// 驗證非法商品更新會被拒絕，
    /// 且不會修改既有商品資料。
    /// </summary>
    [Fact]
    public async Task UpdateProduct_ShouldPreserveExistingProduct_WhenRequestIsInvalid()
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // 先透過正常 POST 建立一筆合法商品。
        using var createResponse =
            await client.PostAsJsonAsync(
                AdminProductUrl,
                CreateProductRequest()
            );

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode
        );

        var originalProduct =
            await createResponse.Content
                .ReadFromJsonAsync<AdminProductDto>();

        Assert.NotNull(originalProduct);

        var productUrl =
            $"{AdminProductUrl}/{originalProduct!.Id}";

        // 建立非法更新 Request。
        //
        // Price = -100 不符合 Range Validation。
        //
        // 同時提供不同的 Title 和 Stock，
        // 確認其他合法欄位也不會被部分更新。
        var invalidRequest = CreateProductRequest(
            title: "Unexpected New Title",
            price: -100m,
            stock: 99
        );


        // Act
        using var response =
            await client.PutAsJsonAsync(
                productUrl,
                invalidRequest
            );


        // Assert：Validation Response
        await AssertValidationErrorAsync(
            response,
            nameof(AdminProductWriteRequest.Price),
            "price 必須介於 0 到 99999999。"
        );


        // Assert：HTTP Product State

        // 再次 GET 商品，
        // 確認原本所有 DTO 欄位都維持不變。
        using var getResponse =
            await client.GetAsync(productUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode
        );

        var savedProduct =
            await getResponse.Content
                .ReadFromJsonAsync<AdminProductDto>();

        Assert.NotNull(savedProduct);

        // 沿用 Step 16-16 的 Record Value Equality。
        Assert.Equal(
            originalProduct,
            savedProduct
        );


        // Assert：Database State
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var products = await db.Products
            .AsNoTracking()
            .ToListAsync();

        var product = Assert.Single(products);

        // 確認仍然是原本的商品。
        Assert.Equal(originalProduct.Id, product.Id);

        // 合法欄位和非法欄位都沒有被修改。
        Assert.Equal("Mechanical Keyboard", product.Title);
        Assert.Equal(1500m, product.Price);
        Assert.Equal(10, product.Stock);

        Assert.Equal(
            originalProduct.CreatedAt,
            product.CreatedAt
        );
    }


    // ==========================================
    // Test 3：Cart POST Validation
    // ==========================================

    /// <summary>
    /// 驗證不合法的購物車 Request，
    /// 會在 DTO Validation 階段被拒絕。
    ///
    /// ProductId 必須 >= 1。
    /// Quantity 必須介於 1 到 99。
    /// </summary>
    [Theory]
    [InlineData(
        0,
        1,
        nameof(AddCartItemRequest.ProductId),
        "productId 必須大於或等於 1。")]
    [InlineData(
        1,
        0,
        nameof(AddCartItemRequest.Quantity),
        "quantity 必須介於 1 到 99。")]
    [InlineData(
        1,
        100,
        nameof(AddCartItemRequest.Quantity),
        "quantity 必須介於 1 到 99。")]
    public async Task AddCartItem_ShouldRejectInvalidRequest(
        int productId,
        int quantity,
        string expectedField,
        string expectedMessage)
    {
        // Arrange
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();


        // 建立測試商品。
        //
        // 刻意將庫存設定為 200，
        // 讓 Quantity = 100 不會超過商品庫存。
        //
        // 如此可以區分：
        //
        // DTO Range Validation
        // 與
        // Business Stock Validation
        using (var seedScope =
            factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            seedDb.Products.Add(
                new Product
                {
                    Id = 1,
                    Title = "Mechanical Keyboard",
                    Price = 1500m,
                    Stock = 200,
                    Category = "electronics"
                }
            );

            await seedDb.SaveChangesAsync();
        }


        // Act
        using var response =
            await client.PostAsJsonAsync(
                CartItemsUrl,
                new AddCartItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            );


        // Assert：Validation Response
        await AssertValidationErrorAsync(
            response,
            expectedField,
            expectedMessage
        );


        // Assert：Database State
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        // 非法 Request 不應建立任何 CartItem。
        Assert.Equal(
            0,
            await db.CartItems.CountAsync()
        );

        // 原本商品仍然存在。
        Assert.Equal(
            1,
            await db.Products.CountAsync()
        );
    }
}
