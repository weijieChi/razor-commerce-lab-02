
using System.Data.Common;
using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// 驗證 CustomWebApplicationFactory
/// 是否成功覆寫正式的 AppDbContext DI 設定。
/// </summary>
public class DatabaseOverrideTests
{
    /// <summary>
    /// 測試 ASP.NET Core Test Host 是否使用
    /// SQLite In-Memory Database。
    ///
    /// 並透過 HTTP Request 確認
    /// Product API 可以正常存取資料庫。
    /// </summary>
    [Fact]
    public async Task TestHost_ShouldUseSqliteInMemoryDatabase()
    {
        // ========================================
        // Arrange：建立 Test Host
        // ========================================

        using var factory =
            new CustomWebApplicationFactory();

        // CreateClient() 會觸發 Test Host 啟動。
        //
        // CustomWebApplicationFactory.CreateHost()
        // 也會初始化 SQLite Schema。
        using var client = factory.CreateClient();


        // ========================================
        // Act：從 DI 取得 AppDbContext
        // ========================================

        // AppDbContext 是 Scoped Service。
        //
        // 我們目前不是在 HTTP Request 裡面，
        // 因此需要自行建立 Scope。
        using var scope =
            factory.Services.CreateScope();

        // 從 Test Host 的 DI Container
        // 取得 AppDbContext。
        //
        // 這個 DbContext 應該已經被 Override，
        // 不再使用正式專案的 SQLite 檔案。
        var db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        // 取得 DI 裡面註冊的 DbConnection。
        var registeredConnection =
            scope.ServiceProvider
                .GetRequiredService<DbConnection>();

        // 取得 AppDbContext 實際使用的 Connection。
        var contextConnection =
            db.Database.GetDbConnection();


        // ========================================
        // Assert 1：確認 DI Override
        // ========================================

        // 確認 DI Connection 與
        // AppDbContext Connection 是同一個物件。
        //
        // Assert.Same() 比較的是 Reference Identity，
        // 不是單純比較兩個物件的內容。
        Assert.Same(
            registeredConnection,
            contextConnection
        );

        // 確認 Connection 的實際類型為
        // Microsoft.Data.Sqlite.SqliteConnection。

        // 確認 SQLite 使用記憶體資料庫。
        //
        // 如果意外使用：
        // Data/razor-commerce.db
        //
        // 這個 Assertion 就會失敗。

        // 確認 Connection 的實際型別。
        var sqliteConnection =
            Assert.IsType<SqliteConnection>(
                contextConnection
            );

        // 解析 Connection String。
        var connectionSettings =
            new SqliteConnectionStringBuilder(
                sqliteConnection.ConnectionString
            );

        // 確認測試使用 SQLite In-Memory Database。
        Assert.Equal(
            ":memory:",
            connectionSettings.DataSource
        );

        // 確認 Connection 保持開啟。
        Assert.Equal(
            System.Data.ConnectionState.Open,
            sqliteConnection.State
        );



        // ========================================
        // Assert 2：確認資料表正常建立
        // ========================================

        // 確認資料庫可以連線。
        Assert.True(
            await db.Database.CanConnectAsync()
        );

        // 實際查詢 Products 資料表。
        //
        // 這個 Test Database 沒有 Seed 商品，
        // 所以應該是 0 筆。
        //
        // 如果資料表沒有建立，
        // CountAsync() 將拋出資料庫例外。
        Assert.Equal(
            0,
            await db.Products.CountAsync()
        );


        // ========================================
        // Act：發送真正的 HTTP Request
        // ========================================

        // 這次首次呼叫會存取資料庫的 Minimal API。
        //
        // Request：
        // GET /api/products
        //
        // 流程：
        // HttpClient
        //   ↓
        // TestServer
        //   ↓
        // ProductEndpoints
        //   ↓
        // DI AppDbContext
        //   ↓
        // SQLite In-Memory
        using var response = await client.GetAsync(
            "/api/products?page=1&pageSize=12"
        );


        // ========================================
        // Assert 3：確認 API 正常執行
        // ========================================

        // 如果 Product API 可以正常查詢
        // 測試資料庫，應回傳 HTTP 200。
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );
    }
}
