
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Product 資料庫 CRUD Integration Tests。
///
/// 這裡測試的是：
/// EF Core + AppDbContext + SQLite 的實際資料庫行為。
///
/// 每個 Test 都會建立自己的 SQLite In-Memory Database。
/// 不使用正式開發資料庫，也不依賴其他測試。
/// </summary>
public class ProductDatabaseTests
{
    // ==========================================
    // Test 1：Create
    // ==========================================

    /// <summary>
    /// 新增 Product 並呼叫 SaveChangesAsync 後，
    /// 應該可以從另一個 DbContext 查到商品。
    /// </summary>
    [Fact]
    public async Task CreateProduct_ShouldPersistData_WhenSaveChangesIsCalled()
    {
        // ========================================
        // Arrange：建立測試資料庫
        // ========================================

        // 建立獨立的 SQLite In-Memory Database。
        //
        // 使用 using var 確保測試結束後，
        // SqliteTestDatabase.Dispose() 會釋放連線。
        using var database = new SqliteTestDatabase();

        // 保存新增商品的 Primary Key。
        // 稍後會使用另一個 DbContext 重新查詢。
        int productId;


        // ========================================
        // Act：新增商品
        // ========================================

        // 第一個 DbContext 只負責 INSERT。
        using (var db = database.CreateContext())
        {
            // 建立要寫入 SQLite 的 Entity。
            var product = new Product
            {
                Title = "Mechanical Keyboard",
                Description = "Test keyboard",
                Price = 1500m,
                Stock = 10,
                Category = "keyboard"
            };

            // Add() 將 Entity 加入 Change Tracker，
            // 並將 Entity State 標記為 Added。
            //
            // 此時還沒有真正執行 INSERT。
            db.Products.Add(product);

            // 真正將新增資料寫入 SQLite。
            await db.SaveChangesAsync();

            // SQLite 會產生商品的 Primary Key。
            //
            // SaveChangesAsync 成功後，
            // EF Core 會將產生的 Id 回填到 product。
            productId = product.Id;

            // 確認 Id 已產生。
            Assert.True(productId > 0);
        }

        // 第一個 DbContext 已經 Dispose。
        // SQLite Connection 仍由 database 保持開啟。


        // ========================================
        // Assert：重新查詢資料庫
        // ========================================

        using (var db = database.CreateContext())
        {
            // 使用全新的 DbContext 讀取資料。
            //
            // AsNoTracking() 表示這次只讀取資料，
            // 不需要 EF Core 追蹤 Entity 變更。
            //
            // SingleAsync() 要求結果恰好一筆。
            var savedProduct = await db.Products
                .AsNoTracking()
                .SingleAsync(p => p.Id == productId);

            // 確認重新查詢的資料符合預期。
            Assert.Equal(
                "Mechanical Keyboard",
                savedProduct.Title
            );

            Assert.Equal(1500m, savedProduct.Price);
            Assert.Equal(10, savedProduct.Stock);

            // 確認資料表裡目前只有這一筆商品。
            Assert.Equal(1, await db.Products.CountAsync());
        }
    }


    // ==========================================
    // Test 2：Read
    // ==========================================

    /// <summary>
    /// 當資料庫存在不同商品時，
    /// 應能根據 Category 查詢符合條件的 Product。
    /// </summary>
    [Fact]
    public async Task GetProduct_ShouldReturnMatchingRecord_WhenProductExists()
    {
        // ========================================
        // Arrange：建立資料庫與測試商品
        // ========================================

        using var database = new SqliteTestDatabase();

        using (var db = database.CreateContext())
        {
            // 準備兩個不同分類的商品。
            var keyboard = new Product
            {
                Title = "Mechanical Keyboard",
                Description = "Test keyboard",
                Price = 1500m,
                Stock = 10,
                Category = "keyboard"
            };

            var mouse = new Product
            {
                Title = "Wireless Mouse",
                Description = "Test mouse",
                Price = 800m,
                Stock = 20,
                Category = "mouse"
            };

            // AddRange() 一次將多個 Entity 加入追蹤。
            db.Products.AddRange(keyboard, mouse);

            // 實際保存兩筆商品。
            await db.SaveChangesAsync();
        }


        // ========================================
        // Act：依 Category 查詢資料
        // ========================================

        using (var db = database.CreateContext())
        {
            // 查詢 Category = keyboard 的商品。
            //
            // SingleOrDefaultAsync：
            // 0 筆：回傳 null
            // 1 筆：回傳該 Entity
            // 超過 1 筆：拋出例外
            //
            // 本次測試資料只有一筆 keyboard。
            var product = await db.Products
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.Category == "keyboard"
                );


            // ====================================
            // Assert：確認查詢結果
            // ====================================

            // 先確認有找到商品。
            Assert.NotNull(product);

            // 確認不是錯誤的商品。
            Assert.Equal(
                "Mechanical Keyboard",
                product!.Title
            );

            Assert.Equal("keyboard", product.Category);

            // 確認兩筆測試商品都存在資料庫。
            Assert.Equal(2, await db.Products.CountAsync());
        }
    }


    // ==========================================
    // Test 3：Update
    // ==========================================

    /// <summary>
    /// 修改既有 Product 並呼叫 SaveChangesAsync 後，
    /// 應能從新的 DbContext 取得更新後的資料。
    /// </summary>
    [Fact]
    public async Task UpdateProduct_ShouldPersistChanges_WhenSaveChangesIsCalled()
    {
        // ========================================
        // Arrange：建立原始商品
        // ========================================

        using var database = new SqliteTestDatabase();

        int productId;

        using (var db = database.CreateContext())
        {
            var product = new Product
            {
                Title = "Old Keyboard",
                Description = "Original product",
                Price = 1000m,
                Stock = 5,
                Category = "keyboard"
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            productId = product.Id;
        }


        // ========================================
        // Act：修改資料庫中的商品
        // ========================================

        using (var db = database.CreateContext())
        {
            // 重新查詢要修改的 Product。
            //
            // 這裡沒有使用 AsNoTracking()，
            // 因為我們需要 EF Core 追蹤 Entity 的變更。
            var product = await db.Products
                .SingleAsync(p => p.Id == productId);

            // 修改被追蹤 Entity 的屬性。
            product.Title = "Updated Keyboard";
            product.Price = 2000m;
            product.Stock = 30;

            // 因為 product 已經由此 DbContext 追蹤，
            // EF Core 可以偵測屬性變更。
            //
            // 不需要再呼叫 db.Products.Update(product)。
            //
            // SaveChangesAsync 會將變更寫回 SQLite。
            await db.SaveChangesAsync();
        }


        // ========================================
        // Assert：重新查詢更新結果
        // ========================================

        using (var db = database.CreateContext())
        {
            // 使用第三個 DbContext，
            // 避免直接檢查剛剛修改的 C# 物件。
            var updatedProduct = await db.Products
                .AsNoTracking()
                .SingleAsync(p => p.Id == productId);

            // 驗證資料庫中的值已經改變。
            Assert.Equal(
                "Updated Keyboard",
                updatedProduct.Title
            );

            Assert.Equal(2000m, updatedProduct.Price);
            Assert.Equal(30, updatedProduct.Stock);

            // 確認沒有額外建立另一筆商品。
            Assert.Equal(1, await db.Products.CountAsync());
        }
    }


    // ==========================================
    // Test 4：Delete
    // ==========================================

    /// <summary>
    /// 刪除 Product 並呼叫 SaveChangesAsync 後，
    /// 應無法再從 SQLite 查到該商品。
    /// </summary>
    [Fact]
    public async Task DeleteProduct_ShouldRemoveRecord_WhenSaveChangesIsCalled()
    {
        // ========================================
        // Arrange：先建立要刪除的商品
        // ========================================

        using var database = new SqliteTestDatabase();

        int productId;

        using (var db = database.CreateContext())
        {
            var product = new Product
            {
                Title = "Keyboard To Delete",
                Description = "Temporary product",
                Price = 1000m,
                Stock = 5,
                Category = "keyboard"
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            productId = product.Id;
        }


        // ========================================
        // Act：刪除商品
        // ========================================

        using (var db = database.CreateContext())
        {
            // 先從資料庫讀取要刪除的商品。
            var product = await db.Products
                .SingleAsync(p => p.Id == productId);

            // Remove() 將 Entity State 標記為 Deleted。
            //
            // 此時資料庫還沒有真正執行 DELETE。
            db.Products.Remove(product);

            // 真正將 DELETE 套用至 SQLite。
            await db.SaveChangesAsync();
        }


        // ========================================
        // Assert：確認商品已不存在
        // ========================================

        using (var db = database.CreateContext())
        {
            // AnyAsync() 用於判斷是否存在符合條件的資料。
            //
            // 存在：true
            // 不存在：false
            var exists = await db.Products
                .AnyAsync(p => p.Id == productId);

            // 預期商品已不存在。
            Assert.False(exists);

            // 本次資料庫原本只有一筆商品，
            // 刪除後應沒有任何 Product。
            Assert.Equal(0, await db.Products.CountAsync());
        }
    }
}
