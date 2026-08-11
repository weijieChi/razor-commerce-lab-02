using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Api;

/// <summary>
/// 商品相關 Minimal API endpoints。
///
/// 我們把商品 API 從 Program.cs 拆出來，
/// 避免所有 Route 都塞進 Program.cs。
/// </summary>
public static class ProductEndpoints
{
    /// <summary>
    /// 把所有商品 API Route 註冊進 WebApplication。
    ///
    /// Program.cs 之後只需要：
    ///
    /// app.MapProductEndpoints();
    /// </summary>
    public static IEndpointRouteBuilder MapProductEndpoints(
        this IEndpointRouteBuilder app)
    {
        /*
            建立 Route Group：

            /api/products

            之後這個 group 裡：

            MapGet("/")
            → GET /api/products

            MapGet("/{id:int}")
            → GET /api/products/{id}
        */
        var group = app.MapGroup("/api/products");

        group.MapGet("/", GetProductsAsync);

        group.MapGet("/{id:int}", GetProductByIdAsync);

        return app;
    }


    /// <summary>
    /// GET /api/products
    ///
    /// 支援：
    ///
    /// keyword
    /// category
    /// page
    /// pageSize
    ///
    /// 例如：
    ///
    /// GET /api/products?page=1&pageSize=12
    ///
    /// GET /api/products?keyword=phone
    ///
    /// GET /api/products?category=beauty
    /// </summary>
    private static async Task<IResult> GetProductsAsync(
        [AsParameters] ProductQuery query,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // ==========================================
        // Query String defaults
        // ==========================================

        /*
            ProductQuery.Page 是 int?。

            Client 有傳：
            ?page=3
            → query.Page = 3
            → page = 3

            Client 沒傳：
            → query.Page = null
            → page = 1
        */
        var page = query.Page ?? 1;

        /*
            同樣：
            Client 沒傳 pageSize
            → 預設每頁 12 筆。
        */
        var pageSize = query.PageSize ?? 12;

        // ==========================================
        // 1. 驗證分頁參數
        // ==========================================

        /*
            page 不允許 0 或負數。

            例如：

            /api/products?page=0

            應該回傳 HTTP 400。
        */
        if (query.Page < 1)
        {
            return Results.BadRequest(new
            {
                message = "page 必須大於或等於 1。"
            });
        }

        /*
            pageSize 限制在 1 ~ 100。

            避免 Client 一次要求：

            pageSize=999999

            把大量資料一次撈出來。
        */
        if (query.PageSize < 1 || query.PageSize > 100)
        {
            return Results.BadRequest(new
            {
                message = "pageSize 必須介於 1 到 100。"
            });
        }


        // ==========================================
        // 2. 建立基本查詢
        // ==========================================

        /*
            db.Products 是 DbSet<Product>。

            AsNoTracking() 表示：

            「這些 Product 只是拿來讀，
              不準備修改並 SaveChanges。」

            EF Core 因此不用追蹤每個 Entity 的狀態。

            對純查詢 API 比較適合。
        */
        var productsQuery =
            db.Products
              .AsNoTracking()
              .AsQueryable();


        // ==========================================
        // 3. Keyword 搜尋
        // ==========================================

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            /*
                Trim() 去除使用者輸入前後空白。

                例如：

                "  phone  "
                     ↓
                "phone"
            */
            var keyword = query.Keyword.Trim();

            /*
                目前搜尋：

                Title
                Description
                Brand

                EF Core 會把這段 LINQ
                轉換成 SQLite SQL。
            */
            productsQuery =
                productsQuery.Where(product =>
                    product.Title.Contains(keyword) ||
                    product.Description.Contains(keyword) ||
                    product.Brand.Contains(keyword));
        }


        // ==========================================
        // 4. Category 篩選
        // ==========================================

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();

            productsQuery =
                productsQuery.Where(product =>
                    product.Category == category);
        }


        // ==========================================
        // 5. 計算符合條件的總筆數
        // ==========================================

        /*
            一定要在 Skip / Take 之前 Count。

            假設：

            符合搜尋條件共 53 筆

            pageSize = 12

            TotalItems = 53

            TotalPages = 5
        */
        var totalItems =
            await productsQuery.CountAsync(cancellationToken);


        // ==========================================
        // 6. 計算總頁數
        // ==========================================

        /*
            Math.Ceiling() 無條件向上取整。

            例如：

            53 / 12 = 4.416...

            → 5 頁
        */
        var totalPages =
            totalItems == 0
                ? 0
                : (int)Math.Ceiling(
                    totalItems / (double)pageSize);


        // ==========================================
        // 7. 分頁 + DTO Projection
        // ==========================================

        var products =
            await productsQuery

                /*
                    先固定排序。

                    分頁一定最好有穩定排序，
                    否則不同 Query 的資料順序
                    可能不穩定。
                */
                .OrderBy(product => product.Id)

                /*
                    page = 1
                    pageSize = 12

                    Skip(0)

                    page = 2
                    Skip(12)

                    page = 3
                    Skip(24)
                */
                // 注意：這裡改用已經套用 default 的 page。
                .Skip((page - 1) * pageSize)

                /*
                    只拿這一頁需要的資料。
                */
                .Take(pageSize)

                /*
                    Select() 在這裡不是單純做 C# object mapping。

                    EF Core 可以把這個 projection
                    轉成 SELECT 需要的 columns。

                    也就是不一定會把 Product 所有欄位
                    都從 DB 撈出來。
                */
                .Select(product => new ProductDto(
                    product.Id,
                    product.Title,
                    product.Description,
                    product.Price,
                    product.DiscountPercentage,
                    product.Rating,
                    product.Stock,
                    product.Brand,
                    product.Category,
                    product.ThumbnailUrl,
                    product.IsFeatured
                ))

                .ToListAsync(cancellationToken);


        // ==========================================
        // 8. 組成分頁 Response
        // ==========================================

        var response =
            new PagedResult<ProductDto>(
                products,
                page,
                pageSize,
                totalItems,
                totalPages
            );


        // HTTP 200 OK
        return Results.Ok(response);
    }


    /// <summary>
    /// GET /api/products/{id}
    ///
    /// 例如：
    ///
    /// GET /api/products/10
    /// </summary>
    private static async Task<IResult> GetProductByIdAsync(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        /*
            找 Id = route parameter 的商品。

            因為只是查詢，所以一樣 AsNoTracking()。
        */
        var product =
            await db.Products
                .AsNoTracking()
                .Where(product => product.Id == id)

                /*
                    直接 Projection 成 ProductDto。
                */
                .Select(product => new ProductDto(
                    product.Id,
                    product.Title,
                    product.Description,
                    product.Price,
                    product.DiscountPercentage,
                    product.Rating,
                    product.Stock,
                    product.Brand,
                    product.Category,
                    product.ThumbnailUrl,
                    product.IsFeatured
                ))
                .FirstOrDefaultAsync(cancellationToken);


        /*
            找不到：

            product == null
            ↓
            HTTP 404
        */
        if (product is null)
        {
            return Results.NotFound(new
            {
                message = $"找不到 Id = {id} 的商品。"
            });
        }


        // 找到商品：
        // HTTP 200 + ProductDto JSON
        return Results.Ok(product);
    }
}
