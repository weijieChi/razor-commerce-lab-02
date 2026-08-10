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
        /*
            到程式執行進入這個 handler 時，

            ProductQuery 已經經過：

            Query String
                 ↓
            Parameter Binding
                 ↓
            DataAnnotations Validation
                 ↓
            GetProductsAsync()

            所以這裡不再需要：

            if (query.Page < 1)
            if (query.PageSize > 100)

            這種基本 request validation。
        */


        // ==========================================
        // 1. 建立基本商品查詢
        // ==========================================

        /*
            GET API 只讀資料，
            不需要 EF Core Change Tracking。
        */
        var productsQuery =
            db.Products
              .AsNoTracking()
              .AsQueryable();


        // ==========================================
        // 2. Keyword 搜尋
        // ==========================================

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();

            productsQuery =
                productsQuery.Where(product =>
                    product.Title.Contains(keyword) ||
                    product.Description.Contains(keyword) ||
                    product.Brand.Contains(keyword));
        }


        // ==========================================
        // 3. Category 篩選
        // ==========================================

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();

            productsQuery =
                productsQuery.Where(product =>
                    product.Category == category);
        }


        // ==========================================
        // 4. 計算符合條件的總筆數
        // ==========================================

        var totalItems =
            await productsQuery.CountAsync(cancellationToken);


        // ==========================================
        // 5. 計算總頁數
        // ==========================================

        var totalPages =
            totalItems == 0
                ? 0
                : (int)Math.Ceiling(
                    totalItems / (double)query.PageSize);


        // ==========================================
        // 6. 排序 + 分頁 + DTO Projection
        // ==========================================

        var products =
            await productsQuery
                .OrderBy(product => product.Id)

                /*
                    Page 1：
                    Skip((1 - 1) * 12)
                    → Skip(0)

                    Page 2：
                    Skip((2 - 1) * 12)
                    → Skip(12)
                */
                .Skip((query.Page - 1) * query.PageSize)

                .Take(query.PageSize)

                /*
                    Product Entity
                         ↓
                    ProductDto

                    API 不直接暴露 Entity。
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
        // 7. Response
        // ==========================================

        var response =
            new PagedResult<ProductDto>(
                products,
                query.Page,
                query.PageSize,
                totalItems,
                totalPages
            );

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
