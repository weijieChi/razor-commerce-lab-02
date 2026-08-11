using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Services;

namespace RazorCommerceLab.Api;

/// <summary>
/// 建立開發 / 練習用假資料的 API。
///
/// 目前只有：
///
/// POST /api/seed/products
/// </summary>
public static class SeedEndpoints
{
    /// <summary>
    /// 註冊 Seed endpoints。
    /// </summary>
    public static IEndpointRouteBuilder MapSeedEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/seed");

        /*
            MapPost("/products")

            搭配 group：

            /api/seed
                +
            /products

            最後：

            POST /api/seed/products
        */
        group.MapPost(
            "/products",
            SeedProductsAsync);

        return app;
    }


    /// <summary>
    /// 從 DummyJSON 匯入商品至本機 SQLite。
    /// </summary>
    private static async Task<IResult> SeedProductsAsync(
        DummyJsonProductClient dummyJsonClient,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        /*
            這次專案固定匯入 100 筆。

            不直接把 DummyJSON 所有商品全部抓回來，
            讓教學資料量保持固定。
        */
        const int importLimit = 100;

        DummyJsonProductsResponse? response;

        try
        {
            // ======================================
            // 1. 呼叫 DummyJSON
            // ======================================

            response =
                await dummyJsonClient.GetProductsAsync(
                    importLimit,
                    cancellationToken);
        }
        catch (HttpRequestException)
        {
            /*
                DNS、連線或 HTTP request 等問題。

                502 Bad Gateway 表示：
                我們自己的 Server 正常，
                但上游 DummyJSON 發生問題。
            */
            return Results.Problem(
                title: "DummyJSON request failed.",
                detail: "無法從 DummyJSON 取得商品資料。",
                statusCode:
                    StatusCodes.Status502BadGateway);
        }
        catch (JsonException)
        {
            /*
                HTTP 回來了，
                但 JSON 無法解析成我們定義的 DTO。
            */
            return Results.Problem(
                title: "DummyJSON response is invalid.",
                detail: "DummyJSON 回傳的資料格式無法解析。",
                statusCode:
                    StatusCodes.Status502BadGateway);
        }
        catch (NotSupportedException)
        {
            /*
                例如上游回傳的內容不是可處理的 JSON。
            */
            return Results.Problem(
                title: "DummyJSON response is invalid.",
                detail: "DummyJSON 沒有回傳預期的 JSON。",
                statusCode:
                    StatusCodes.Status502BadGateway);
        }


        // 理論上正常 API response 應該不會是 null。
        if (response is null)
        {
            return Results.Problem(
                title: "DummyJSON returned no data.",
                detail: "DummyJSON 沒有回傳商品資料。",
                statusCode:
                    StatusCodes.Status502BadGateway);
        }


        // ==========================================
        // 2. 找出這次 DummyJSON 的 ExternalId
        // ==========================================

        /*
            DummyJSON：

            id = 1
            id = 2
            id = 3

            對我們而言：

            ExternalId = 1
            ExternalId = 2
            ExternalId = 3
        */
        var externalIds =
            response.Products
                .Select(product => product.Id)
                .ToList();


        // ==========================================
        // 3. 查 DB 已存在的 ExternalId
        // ==========================================

        /*
            不要每一筆商品都：

            AnyAsync()
            AnyAsync()
            AnyAsync()
            ...

            那樣會造成很多次 DB query。

            我們一次把已存在的 ExternalId 查出來。
        */
        var existingExternalIds =
            await db.Products
                .AsNoTracking()
                .Where(product =>
                    product.ExternalId.HasValue &&
                    externalIds.Contains(
                        product.ExternalId.Value))
                .Select(product =>
                    product.ExternalId!.Value)
                .ToListAsync(cancellationToken);

        /*
            HashSet 適合反覆做 Contains()：

            這個 ExternalId 已存在嗎？
        */
        var existingExternalIdSet =
            existingExternalIds.ToHashSet();


        // ==========================================
        // 4. DTO → Entity Mapping
        // ==========================================

        var newProducts =
            response.Products

                /*
                    已存在的 DummyJSON 商品跳過。

                    這樣重複呼叫 seed API
                    不會一直新增 duplicate。
                */
                .Where(source =>
                    !existingExternalIdSet.Contains(source.Id))

                /*
                    DummyJsonProductDto
                            ↓
                    Product Entity
                */
                .Select(source => new Product
                {
                    /*
                        不要設定 Product.Id。

                        Product.Id 是 SQLite
                        自己產生的 Primary Key。
                    */

                    // DummyJSON id
                    ExternalId = source.Id,

                    Title =
                        source.Title ?? string.Empty,

                    Description =
                        source.Description ?? string.Empty,

                    Price =
                        source.Price,

                    DiscountPercentage =
                        source.DiscountPercentage,

                    Rating =
                        source.Rating,

                    Stock =
                        source.Stock,

                    Brand =
                        source.Brand ?? string.Empty,

                    Category =
                        source.Category ?? string.Empty,

                    /*
                        如果 DummyJSON 沒 thumbnail，
                        DB 就保留空字串。

                        Step 5 UI 才處理：

                        空圖片
                          ↓
                        picsum.photos
                          ↓ 失敗
                        product-placeholder.svg
                    */
                    ThumbnailUrl =
                        source.Thumbnail ?? string.Empty,

                    /*
                        External API 是：

                        List<string>

                        但 DB 欄位是：

                        string ImageUrlsJson

                        所以先 serialize 成 JSON 字串。
                    */
                    ImageUrlsJson =
                        JsonSerializer.Serialize(
                            source.Images ?? []),

                    /*
                        這是「我們自己專案的 seed 規則」。

                        DummyJSON 本身沒有
                        IsFeatured 欄位。

                        先把 DummyJSON id 1~8
                        當作首頁精選商品。

                        這樣 Step 5 做首頁時
                        馬上有資料可以顯示。
                    */
                    IsFeatured =
                        source.Id is >= 1 and <= 8,

                    CreatedAt =
                        DateTime.UtcNow
                })

                .ToList();


        // ==========================================
        // 5. 新增至 DbContext
        // ==========================================

        /*
            AddRange()：

            告訴 EF Core：

            這些 Product 都是新增資料。

            目前還沒有真正 INSERT SQLite。
        */
        db.Products.AddRange(newProducts);


        // ==========================================
        // 6. 寫入 SQLite
        // ==========================================

        /*
            SaveChangesAsync() 才真正把：

            Added Product
                ↓
            INSERT INTO Products ...
                ↓
            SQLite
        */
        await db.SaveChangesAsync(cancellationToken);


        // ==========================================
        // 7. API Response
        // ==========================================

        return Results.Ok(new
        {
            requested = importLimit,

            /*
                DummyJSON 實際回來幾筆。
            */
            received = response.Products.Count,

            /*
                這次實際 INSERT 幾筆。
            */
            inserted = newProducts.Count,

            /*
                因為 ExternalId 已存在而跳過幾筆。
            */
            skipped =
                response.Products.Count -
                newProducts.Count
        });
    }
}
