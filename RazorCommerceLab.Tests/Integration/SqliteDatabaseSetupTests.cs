
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// 驗證 SQLite 測試資料庫是否能正確初始化。
///
/// 與 ProductFormHelper Unit Test 不同，
/// 這裡真的會使用 EF Core 和 SQLite。
///
/// 因此屬於 Integration Test。
/// </summary>
public class SqliteDatabaseSetupTests
{
    /// <summary>
    /// 驗證測試資料庫建立後：
    ///
    /// 1. 可以正常連線。
    /// 2. Products 資料表可以查詢。
    /// 3. CartItems 資料表可以查詢。
    /// 4. 不同 DbContext 可以使用同一個測試 DB。
    /// </summary>
    [Fact]
    public void CreateTestDatabase_ShouldInitializeSchema_WhenConnectionIsOpen()
    {
        // ========================================
        // Arrange
        // ========================================

        // 建立一個全新的 SQLite In-Memory DB。
        //
        // 這個物件會負責：
        // - 開啟 SQLite Connection
        // - 建立 EF Core Schema
        // - 測試結束後釋放 Connection
        using var database = new SqliteTestDatabase();


        // ========================================
        // Act + Assert：第一個 DbContext
        // ========================================

        // CreateContext() 建立第一個 DbContext。
        using (var db = database.CreateContext())
        {
            // 確認目前 SQLite 可以連線。
            Assert.True(db.Database.CanConnect());

            // 查詢空的 Products 資料表。
            //
            // 如果資料表不存在，
            // 查詢會發生 SQLite Exception，
            // 測試就會失敗。
            Assert.Empty(db.Products);

            // CartItems 也應該可以正常查詢。
            Assert.Empty(db.CartItems);
        }

        // 第一個 DbContext 已經 Dispose。
        //
        // 但是 SqliteTestDatabase 持有的
        // Connection 仍然保持開啟。


        // ========================================
        // Act + Assert：第二個 DbContext
        // ========================================

        // 建立另一個全新的 DbContext。
        using (var db = database.CreateContext())
        {
            // 同樣可以查詢原本的資料表。
            //
            // 這驗證 SQLite Connection
            // 沒有因第一個 DbContext Dispose
            // 就造成 In-Memory Schema 消失。
            Assert.Empty(db.Products);
            Assert.Empty(db.CartItems);
        }

        // 測試結束後：
        //
        // database.Dispose()
        //
        // SQLite Connection 關閉，
        // In-Memory DB 隨之消失。
    }
}
