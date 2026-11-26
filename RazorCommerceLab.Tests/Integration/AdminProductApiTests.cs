
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Admin Product API Integration Tests。
///
/// 驗證商品的完整生命週期：
///
/// Create → Read → Update → Read → Delete → Not Found
///
/// 同時驗證：
///
/// 1. HTTP Status Code
/// 2. Location Response Header
/// 3. JSON Response DTO
/// 4. 商品欄位正規化
/// 5. Database Persistence
/// 6. 系統管理欄位保持不變
///
/// 每個 Test 使用獨立的 Test Host
/// 與 SQLite In-Memory Database。
/// </summary>
public class AdminProductApiTests
{
    private const string BaseUrl =
        "/api/admin/products";


    // ==========================================
    // HTTP Response Helper
    // ==========================================

    /// <summary>
    /// 驗證 HTTP Status Code，
    /// 並將 JSON Response 反序列化為 AdminProductDto。
    ///
    /// 因為 POST 回傳 201，
    /// GET 和 PUT 回傳 200，
    /// 所以將預期的 Status Code 作為參數傳入。
    /// </summary>
    private static async Task<AdminProductDto>
        ReadProductResponseAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(
            expectedStatusCode,
            response.StatusCode
        );

        var product = await response.Content
            .ReadFromJsonAsync<AdminProductDto>();

        Assert.NotNull(product);

        return product!;
    }


    // ==========================================
    // Database Assertion Helper
    // ==========================================

    /// <summary>
    /// 使用新的 DI Scope 和 AppDbContext，
    /// 直接讀取 Products 資料表。
    ///
    /// 每個測試都有獨立資料庫，
    /// 因此可以直接查詢所有商品，
    /// 確認是否有非預期的新增或刪除紀錄。
    /// </summary>
    private static async Task<List<Product>>
        ReadProductsFromDatabaseAsync(
            CustomWebApplicationFactory factory)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await db.Products
            .AsNoTracking()
            .OrderBy(product => product.Id)
            .ToListAsync();
    }


    // ==========================================
    // Test 1：Product Lifecycle
    // ==========================================

    /// <summary>
    /// 驗證後台商品的完整生命週期。
    ///
    /// 所有商品操作都透過 HTTP API 完成，
    /// 不直接使用 AppDbContext 新增、修改或刪除。
    ///
    /// AppDbContext 只負責驗證資料庫狀態。
    /// </summary>
    [Fact]
    public async Task ProductLifecycle_ShouldCreateReadUpdateAndDeleteProduct()
    {
        // ========================================
        // Arrange
        // ========================================

        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        // 本次不需要 Seed Data。
        //
        // 因為商品將由 POST API 建立，
        // 所以測試從空資料庫開始。
        var initialProducts =
            await ReadProductsFromDatabaseAsync(factory);

        Assert.Empty(initialProducts);


        // ========================================
        // Act 1：Create Product
        // POST /api/admin/products
        // ========================================

        var createRequest =
            new AdminProductWriteRequest
            {
                // 刻意加入前後空白，
                // 驗證 Endpoint 內的 Trim() 行為。
                Title = "  Mechanical Keyboard  ",
                Description = "  Test keyboard  ",

                Price = 1500m,
                DiscountPercentage = 10m,
                Rating = 4.5m,
                Stock = 10,

                // 選填欄位使用 null，
                // 驗證 Server 是否轉換成空字串。
                Brand = null,

                Category = "  electronics  ",
                ThumbnailUrl = null,

                IsFeatured = false
            };

        using var createResponse =
            await client.PostAsJsonAsync(
                BaseUrl,
                createRequest
            );


        // ========================================
        // Assert 1：HTTP 201 Created
        // ========================================

        var createdProduct =
            await ReadProductResponseAsync(
                createResponse,
                HttpStatusCode.Created
            );

        // Id 由資料庫產生，
        // 不在測試中預先指定。
        Assert.True(createdProduct.Id > 0);

        // 人工建立的商品沒有 DummyJSON ExternalId。
        Assert.Null(createdProduct.ExternalId);

        // 驗證字串正規化。
        Assert.Equal(
            "Mechanical Keyboard",
            createdProduct.Title
        );

        Assert.Equal(
            "Test keyboard",
            createdProduct.Description
        );

        Assert.Equal(
            "electronics",
            createdProduct.Category
        );

        // 驗證數值欄位。
        Assert.Equal(1500m, createdProduct.Price);
        Assert.Equal(10m, createdProduct.DiscountPercentage);
        Assert.Equal(4.5m, createdProduct.Rating);
        Assert.Equal(10, createdProduct.Stock);

        // null 應由 Endpoint 轉換成空字串。
        Assert.Equal(string.Empty, createdProduct.Brand);
        Assert.Equal(string.Empty, createdProduct.ThumbnailUrl);

        Assert.False(createdProduct.IsFeatured);

        // 保存 Server 實際產生的商品 Id。
        var productId = createdProduct.Id;

        var productUrl =
            $"{BaseUrl}/{productId}";


        // ========================================
        // Assert 2：Location Header
        // ========================================

        // POST 成功後，Results.Created()
        // 應回傳新資源的 Location Header。
        Assert.NotNull(
            createResponse.Headers.Location
        );

        Assert.Equal(
            productUrl,
            createResponse.Headers.Location!.OriginalString
        );


        // ========================================
        // Assert 3：Create Database State
        // ========================================

        var createdRows =
            await ReadProductsFromDatabaseAsync(factory);

        var createdRow = Assert.Single(createdRows);

        Assert.Equal(productId, createdRow.Id);
        Assert.Equal("Mechanical Keyboard", createdRow.Title);
        Assert.Equal(1500m, createdRow.Price);
        Assert.Equal(10, createdRow.Stock);

        // 驗證由 Server 管理的欄位。
        Assert.Null(createdRow.ExternalId);
        Assert.Equal("[]", createdRow.ImageUrlsJson);

        Assert.NotEqual(
            default(DateTime),
            createdRow.CreatedAt
        );

        Assert.Equal(
            createdProduct.CreatedAt,
            createdRow.CreatedAt
        );


        // ========================================
        // Act 2：Read Product
        // GET /api/admin/products/{id}
        // ========================================

        using var getResponse =
            await client.GetAsync(productUrl);

        var fetchedProduct =
            await ReadProductResponseAsync(
                getResponse,
                HttpStatusCode.OK
            );


        // ========================================
        // Assert 4：Read Created Product
        // ========================================

        // AdminProductDto 是 record。
        //
        // record 預設提供 Value Equality，
        // 因此可以直接比較兩個 DTO 的資料內容，
        // 不需要再次逐一比較每個 Property。
        Assert.Equal(
            createdProduct,
            fetchedProduct
        );


        // ========================================
        // Act 3：Update Product
        // PUT /api/admin/products/{id}
        // ========================================

        // 修改所有可編輯欄位，
        // 以驗證 PUT 是否正確套用新的資料。
        var updateRequest =
            new AdminProductWriteRequest
            {
                Title = "  Gaming Keyboard  ",
                Description = "  Updated keyboard  ",

                Price = 2000m,
                DiscountPercentage = 20m,
                Rating = 4.8m,
                Stock = 25,

                Brand = "  Gaming Brand  ",
                Category = "  gaming  ",

                ThumbnailUrl =
                    "  https://example.com/keyboard.jpg  ",

                IsFeatured = true
            };

        using var updateResponse =
            await client.PutAsJsonAsync(
                productUrl,
                updateRequest
            );

        var updatedProduct =
            await ReadProductResponseAsync(
                updateResponse,
                HttpStatusCode.OK
            );


        // ========================================
        // Assert 5：Update Response
        // ========================================

        // Id 必須維持不變。
        Assert.Equal(productId, updatedProduct.Id);

        // 驗證所有可編輯欄位。
        Assert.Equal(
            "Gaming Keyboard",
            updatedProduct.Title
        );

        Assert.Equal(
            "Updated keyboard",
            updatedProduct.Description
        );

        Assert.Equal(2000m, updatedProduct.Price);
        Assert.Equal(20m, updatedProduct.DiscountPercentage);
        Assert.Equal(4.8m, updatedProduct.Rating);
        Assert.Equal(25, updatedProduct.Stock);

        Assert.Equal(
            "Gaming Brand",
            updatedProduct.Brand
        );

        Assert.Equal(
            "gaming",
            updatedProduct.Category
        );

        Assert.Equal(
            "https://example.com/keyboard.jpg",
            updatedProduct.ThumbnailUrl
        );

        Assert.True(updatedProduct.IsFeatured);

        // 系統管理欄位不應被正常 PUT 操作改變。
        Assert.Equal(
            createdProduct.ExternalId,
            updatedProduct.ExternalId
        );

        Assert.Equal(
            createdProduct.CreatedAt,
            updatedProduct.CreatedAt
        );


        // ========================================
        // Act 4：Read Updated Product
        // ========================================

        using var updatedGetResponse =
            await client.GetAsync(productUrl);

        var fetchedUpdatedProduct =
            await ReadProductResponseAsync(
                updatedGetResponse,
                HttpStatusCode.OK
            );

        // 再次 GET，確認修改結果確實被保存。
        Assert.Equal(
            updatedProduct,
            fetchedUpdatedProduct
        );


        // ========================================
        // Assert 6：Update Database State
        // ========================================

        var updatedRows =
            await ReadProductsFromDatabaseAsync(factory);

        // 更新商品不應產生新的紀錄。
        var updatedRow = Assert.Single(updatedRows);

        Assert.Equal(createdRow.Id, updatedRow.Id);

        Assert.Equal(
            "Gaming Keyboard",
            updatedRow.Title
        );

        Assert.Equal(2000m, updatedRow.Price);
        Assert.Equal(25, updatedRow.Stock);
        Assert.True(updatedRow.IsFeatured);

        // 驗證未開放修改的 Entity 欄位。
        Assert.Equal(
            createdRow.ExternalId,
            updatedRow.ExternalId
        );

        Assert.Equal(
            createdRow.ImageUrlsJson,
            updatedRow.ImageUrlsJson
        );

        Assert.Equal(
            createdRow.CreatedAt,
            updatedRow.CreatedAt
        );


        // ========================================
        // Act 5：Delete Product
        // DELETE /api/admin/products/{id}
        // ========================================

        using var deleteResponse =
            await client.DeleteAsync(productUrl);


        // ========================================
        // Assert 7：HTTP 204 No Content
        // ========================================

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode
        );

        // 204 Response 不應包含 Response Body。
        var deleteBody = await deleteResponse.Content
            .ReadAsStringAsync();

        Assert.Empty(deleteBody);


        // ========================================
        // Act 6：Read Deleted Product
        // ========================================

        using var notFoundResponse =
            await client.GetAsync(productUrl);


        // ========================================
        // Assert 8：HTTP 404 Not Found
        // ========================================

        // 商品已經被刪除，
        // 所以 HTTP 404 才是正確的結果。
        Assert.Equal(
            HttpStatusCode.NotFound,
            notFoundResponse.StatusCode
        );


        // ========================================
        // Assert 9：Final Database State
        // ========================================

        var finalProducts =
            await ReadProductsFromDatabaseAsync(factory);

        // 確認商品紀錄確實從資料庫刪除。
        Assert.Empty(finalProducts);
    }
}
