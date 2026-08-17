using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Api.Dtos;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Api;


/// <summary>
/// 後台商品 CRUD Minimal API。
///
/// GET    /api/admin/products/{id}
/// POST   /api/admin/products
/// PUT    /api/admin/products/{id}
/// DELETE /api/admin/products/{id}
///
/// 注意：
///
/// 目前專案沒有登入 / Authorization。
///
/// 所以這個 /api/admin/... 路徑
/// 只是程式結構上的「後台 API」。
///
/// 它本身不代表有權限保護。
/// </summary>
public static class AdminProductEndpoints
{
    // ==========================================
    // Route registration
    // ==========================================

    public static IEndpointRouteBuilder
        MapAdminProductEndpoints(
            this IEndpointRouteBuilder app)
    {
        /*
            共用 Route Prefix：

            /api/admin/products
        */
        var group =
            app.MapGroup(
                "/api/admin/products");


        /*
            READ

            GET /api/admin/products/5
        */
        group.MapGet(
            "/{id:int}",
            GetProductAsync);


        /*
            CREATE

            POST /api/admin/products
        */
        group.MapPost(
            "/",
            CreateProductAsync);


        /*
            UPDATE

            PUT /api/admin/products/5
        */
        group.MapPut(
            "/{id:int}",
            UpdateProductAsync);


        /*
            DELETE

            DELETE /api/admin/products/5
        */
        group.MapDelete(
            "/{id:int}",
            DeleteProductAsync);


        return app;
    }


    // ==========================================
    // GET /api/admin/products/{id}
    // ==========================================

    /// <summary>
    /// 讀取單一商品。
    /// </summary>
    private static async Task<IResult> GetProductAsync(
        /*
            Route：

            /api/admin/products/5
                                ↑
                                id

            :int 負責 Route Binding。

            [Range] 則負責：
            id 必須 >= 1。
        */
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "id 必須大於或等於 1。")]
        int id,

        AppDbContext db,

        CancellationToken cancellationToken)
    {
        /*
            GET 只讀資料，
            所以使用 AsNoTracking()。
        */
        var product =
            await db.Products
                .AsNoTracking()

                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        if (product is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"找不到 Id = {id} 的商品。"
                });
        }


        /*
            Entity
              ↓
            AdminProductDto
        */
        return Results.Ok(
            ToDto(product));
    }


    // ==========================================
    // POST /api/admin/products
    // ==========================================

    /// <summary>
    /// 建立新商品。
    /// </summary>
    private static async Task<IResult> CreateProductAsync(
        /*
            JSON Request Body
                  ↓
            AdminProductWriteRequest
                  ↓
            Minimal API Validation
        */
        [FromBody]
        AdminProductWriteRequest request,

        AppDbContext db,

        CancellationToken cancellationToken)
    {
        /*
            到這個 handler 時：

            [Required]
            [StringLength]
            [Range]

            都已經通過。

            所以不需要再手寫：

            if (request.Price < 0)
            if (request.Stock < 0)
        */


        // ======================================
        // DTO → Entity
        // ======================================

        var product =
            new Product
            {
                /*
                    Admin 人工建立商品，
                    沒有 DummyJSON ExternalId。
                */
                ExternalId = null,


                /*
                    Trim()：

                    去掉前後不必要空白。

                    "  Keyboard  "
                    ↓
                    "Keyboard"
                */
                Title =
                    request.Title.Trim(),

                Description =
                    request.Description.Trim(),

                Price =
                    request.Price,

                DiscountPercentage =
                    request.DiscountPercentage,

                Rating =
                    request.Rating,

                Stock =
                    request.Stock,

                Brand =
                    request.Brand?.Trim()
                    ?? string.Empty,

                Category =
                    request.Category.Trim(),

                ThumbnailUrl =
                    request.ThumbnailUrl?.Trim()
                    ?? string.Empty,


                /*
                    本次 Admin CRUD
                    暫時沒有圖片 Gallery UI。

                    新商品直接使用空 JSON array。
                */
                ImageUrlsJson = "[]",


                IsFeatured =
                    request.IsFeatured,


                CreatedAt =
                    DateTime.UtcNow
            };


        // ======================================
        // INSERT
        // ======================================

        /*
            Add() 本身只是把 Entity
            標記成 Added。
        */
        db.Products.Add(
            product);


        /*
            SaveChangesAsync()
            才真正執行 INSERT。
        */
        await db.SaveChangesAsync(
            cancellationToken);


        // ======================================
        // HTTP 201 Created
        // ======================================

        /*
            建立成功後：

            Status Code：
            201 Created

            Location：
            /api/admin/products/{newId}

            Body：
            新建立的 Product DTO
        */
        return Results.Created(
            $"/api/admin/products/{product.Id}",
            ToDto(product));
    }


    // ==========================================
    // PUT /api/admin/products/{id}
    // ==========================================

    /// <summary>
    /// 完整更新一筆商品的可編輯欄位。
    /// </summary>
    private static async Task<IResult> UpdateProductAsync(
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "id 必須大於或等於 1。")]
        int id,

        [FromBody]
        AdminProductWriteRequest request,

        AppDbContext db,

        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. 找 Product
        // ======================================

        /*
            這裡刻意沒有 AsNoTracking()。

            因為我們等等要修改這個 Entity。

            EF Core 需要追蹤它的狀態。
        */
        var product =
            await db.Products
                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        if (product is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"找不到 Id = {id} 的商品。"
                });
        }


        // ======================================
        // 2. 修改 editable fields
        // ======================================

        product.Title =
            request.Title.Trim();

        product.Description =
            request.Description.Trim();

        product.Price =
            request.Price;

        product.DiscountPercentage =
            request.DiscountPercentage;

        product.Rating =
            request.Rating;

        product.Stock =
            request.Stock;

        product.Brand =
            request.Brand?.Trim()
            ?? string.Empty;

        product.Category =
            request.Category.Trim();

        product.ThumbnailUrl =
            request.ThumbnailUrl?.Trim()
            ?? string.Empty;

        product.IsFeatured =
            request.IsFeatured;


        /*
            注意這裡「沒有修改」：

            product.Id
            product.ExternalId
            product.ImageUrlsJson
            product.CreatedAt

            因為這些不是 Admin
            這次允許修改的欄位。
        */


        // ======================================
        // 3. UPDATE
        // ======================================

        /*
            不需要：

            db.Products.Update(product);

            原因：

            product 是由目前這個 DbContext
            查詢出來的 tracked Entity。

            EF Core 已經知道它原本的狀態。

            修改 property 後：

            SaveChangesAsync()

            就會偵測變更並產生 UPDATE。
        */
        await db.SaveChangesAsync(
            cancellationToken);


        return Results.Ok(
            ToDto(product));
    }


    // ==========================================
    // DELETE /api/admin/products/{id}
    // ==========================================

    /// <summary>
    /// 刪除商品。
    /// </summary>
    private static async Task<IResult> DeleteProductAsync(
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "id 必須大於或等於 1。")]
        int id,

        AppDbContext db,

        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. 找 Product
        // ======================================

        var product =
            await db.Products
                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        if (product is null)
        {
            return Results.NotFound(
                new
                {
                    message =
                        $"找不到 Id = {id} 的商品。"
                });
        }


        // ======================================
        // 2. DELETE
        // ======================================

        /*
            Remove()：

            先把 Entity state
            標記成 Deleted。
        */
        db.Products.Remove(
            product);


        /*
            SaveChangesAsync()
            才真正送出 DELETE。
        */
        await db.SaveChangesAsync(
            cancellationToken);


        /*
            刪除成功沒有其他內容要回傳，
            所以使用：

            HTTP 204 No Content
        */
        return Results.NoContent();
    }


    // ==========================================
    // Entity → DTO
    // ==========================================

    /// <summary>
    /// 統一 Product → AdminProductDto mapping。
    ///
    /// 避免每個 Endpoint 都重複：
    ///
    /// new AdminProductDto(...)
    /// </summary>
    private static AdminProductDto
        ToDto(Product product)
    {
        return new AdminProductDto(
            Id:
                product.Id,

            ExternalId:
                product.ExternalId,

            Title:
                product.Title,

            Description:
                product.Description,

            Price:
                product.Price,

            DiscountPercentage:
                product.DiscountPercentage,

            Rating:
                product.Rating,

            Stock:
                product.Stock,

            Brand:
                product.Brand,

            Category:
                product.Category,

            ThumbnailUrl:
                product.ThumbnailUrl,

            IsFeatured:
                product.IsFeatured,

            CreatedAt:
                product.CreatedAt
        );
    }
}
