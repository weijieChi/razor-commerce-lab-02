
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// 驗證資料庫的完整性規則。
///
/// 本次涵蓋：
/// 1. Foreign Key Constraint
/// 2. Cascade Delete
/// 3. Composite Unique Index
///
/// 每個 Test 都使用獨立的 SQLite In-Memory Database。
/// </summary>
public class DatabaseConstraintTests
{
    // SQLite Extended Result Codes。
    //
    // 787 = SQLITE_CONSTRAINT_FOREIGNKEY
    // 2067 = SQLITE_CONSTRAINT_UNIQUE
    //
    // 這些數字是 SQLite 專用的錯誤碼，
    // 不是 EF Core 的通用錯誤碼。
    private const int ForeignKeyError = 787;
    private const int UniqueConstraintError = 2067;


    // ==========================================
    // Test 1：Foreign Key Constraint
    // ==========================================

    /// <summary>
    /// 當 CartItem 指向不存在的 Product 時，
    /// SQLite 應拒絕新增。
    /// </summary>
    [Fact]
    public async Task AddCartItem_ShouldFail_WhenProductDoesNotExist()
    {
        // ========================================
        // Arrange
        // ========================================

        // 建立獨立的測試資料庫。
        using var database = new SqliteTestDatabase();

        using (var db = database.CreateContext())
        {
            // 測試資料庫目前沒有任何 Product。
            //
            // 但我們故意建立 ProductId = 999999
            // 的 CartItem。
            var cartItem = new CartItem
            {
                CartKey = "cart-fk-test",
                ProductId = 999999,
                Quantity = 1
            };

            // EF Core 將 CartItem 標記為 Added。
            //
            // Add() 本身不會立即執行 INSERT。
            db.CartItems.Add(cartItem);


            // ====================================
            // Act + Assert
            // ====================================

            // SaveChangesAsync() 嘗試執行 INSERT。
            //
            // 因為 ProductId = 999999 不存在，
            // SQLite 應該拒絕這筆資料。
            //
            // EF Core 會將資料庫寫入錯誤
            // 包裝成 DbUpdateException。
            //
            // 如果沒有發生預期 Exception，
            // 這個 Assert 就會失敗。
            var exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => db.SaveChangesAsync()
                );

            // EF Core 的 DbUpdateException
            // 裡面通常包含原始的 SQLite Exception。
            var sqliteException =
                Assert.IsType<SqliteException>(
                    exception.InnerException
                );

            // 確認真正的失敗原因是 Foreign Key，
            // 而不是其他資料庫錯誤。
            Assert.Equal(
                ForeignKeyError,
                sqliteException.SqliteExtendedErrorCode
            );
        }

        // ========================================
        // Assert：確認資料庫沒有新增非法資料
        // ========================================

        // 使用新的 DbContext 查詢資料庫。
        using (var db = database.CreateContext())
        {
            Assert.Empty(db.CartItems);
        }
    }


    /// <summary>
    /// 刪除 Product 時，相關的 CartItem 應自動刪除。
    ///
    /// 這次刻意不載入 CartItems，
    /// 以驗證 SQLite 的 ON DELETE CASCADE。
    /// </summary>
    [Fact]
    public async Task DeleteProduct_ShouldCascadeDeleteCartItems()
    {
        // ========================================
        // Arrange：建立 Product + CartItems
        // ========================================

        using var database = new SqliteTestDatabase();

        int productId;

        using (var db = database.CreateContext())
        {
            // 建立被購物車引用的商品。
            var product = new Product
            {
                Title = "Mechanical Keyboard",
                Description = "Test keyboard",
                Price = 1500m,
                Stock = 10,
                Category = "keyboard"
            };

            db.Products.Add(product);

            // 先保存 Product，
            // 讓資料庫產生 Product.Id。
            await db.SaveChangesAsync();

            productId = product.Id;

            // 模擬兩個不同匿名購物車，
            // 都加入了同一個商品。
            //
            // CartKey 不同，因此不違反
            // (CartKey, ProductId) Unique Index。
            db.CartItems.AddRange(
                new CartItem
                {
                    CartKey = "cart-A",
                    ProductId = productId,
                    Quantity = 2
                },
                new CartItem
                {
                    CartKey = "cart-B",
                    ProductId = productId,
                    Quantity = 3
                }
            );

            // 將兩筆 CartItem 寫入 SQLite。
            await db.SaveChangesAsync();
        }


        // ========================================
        // Act：刪除 Product
        // ========================================

        using (var db = database.CreateContext())
        {
            // 只查詢 Product。
            //
            // 注意：
            // 這裡沒有 Include(CartItems)，
            // 也沒有查詢 db.CartItems。
            //
            // 因此這個 DbContext 不會追蹤
            // 相關的兩筆 CartItem。
            var product = await db.Products
                .SingleAsync(p => p.Id == productId);

            // 只要求刪除 Product。
            db.Products.Remove(product);

            // EF Core 對資料庫送出 DELETE。
            //
            // SQLite 根據 ON DELETE CASCADE
            // 自動刪除相關 CartItems。
            await db.SaveChangesAsync();
        }


        // ========================================
        // Assert：重新查詢資料庫
        // ========================================

        using (var db = database.CreateContext())
        {
            // Product 應該不存在。
            var productExists = await db.Products
                .AnyAsync(p => p.Id == productId);

            Assert.False(productExists);

            // 相關的 CartItems 也應全部消失。
            var cartItemsExist = await db.CartItems
                .AnyAsync(item => item.ProductId == productId);

            Assert.False(cartItemsExist);

            // 本次資料庫原本只有兩筆 CartItem，
            // 所以現在應該是 0 筆。
            Assert.Equal(
                0,
                await db.CartItems.CountAsync()
            );
        }
    }


    /// <summary>
    /// 同一 CartKey + ProductId 不允許重複。
    ///
    /// 這次要驗證的是資料庫 Unique Index，
    /// 而不是 Cart API 的合併數量邏輯。
    /// </summary>
    [Fact]
    public async Task AddCartItem_ShouldFail_WhenCartKeyAndProductIdAlreadyExist()
    {
        // ========================================
        // Arrange：建立商品與第一筆 CartItem
        // ========================================

        using var database = new SqliteTestDatabase();

        int productId;

        using (var db = database.CreateContext())
        {
            // 建立商品。
            var product = new Product
            {
                Title = "Mechanical Keyboard",
                Description = "Test keyboard",
                Price = 1500m,
                Stock = 10,
                Category = "keyboard"
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            productId = product.Id;

            // 第一次加入購物車。
            db.CartItems.Add(new CartItem
            {
                CartKey = "cart-A",
                ProductId = productId,
                Quantity = 2
            });

            await db.SaveChangesAsync();
        }


        // ========================================
        // Act：嘗試新增重複 CartItem
        // ========================================

        using (var db = database.CreateContext())
        {
            // 使用新的 DbContext，
            // 避免前一個 DbContext 的 Change Tracker
            // 影響這次測試。
            //
            // 這筆資料與第一筆具有相同：
            // CartKey + ProductId。
            //
            // 雖然 Quantity 不同，
            // 仍然違反 Composite Unique Index。
            db.CartItems.Add(new CartItem
            {
                CartKey = "cart-A",
                ProductId = productId,
                Quantity = 3
            });

            // 預期 SQLite 拒絕新增重複資料。
            var exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => db.SaveChangesAsync()
                );

            // 取得 SQLite 原始錯誤。
            var sqliteException =
                Assert.IsType<SqliteException>(
                    exception.InnerException
                );

            // 2067 表示 UNIQUE Constraint 失敗。
            Assert.Equal(
                UniqueConstraintError,
                sqliteException.SqliteExtendedErrorCode
            );
        }


        // ========================================
        // Assert：確認資料庫仍只有第一筆
        // ========================================

        using (var db = database.CreateContext())
        {
            // 這裡使用 SingleAsync。
            //
            // 如果存在 0 筆或多筆資料，
            // 都會拋出 Exception。
            var cartItem = await db.CartItems
                .AsNoTracking()
                .SingleAsync(item =>
                    item.CartKey == "cart-A" &&
                    item.ProductId == productId
                );

            // 第一筆的 Quantity 應維持 2。
            //
            // 資料庫不會自動將 2 + 3 合併成 5。
            Assert.Equal(2, cartItem.Quantity);

            // 確認沒有第二筆重複紀錄。
            Assert.Equal(
                1,
                await db.CartItems.CountAsync()
            );
        }
    }

}
