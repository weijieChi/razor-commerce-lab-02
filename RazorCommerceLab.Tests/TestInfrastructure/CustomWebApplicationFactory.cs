
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Tests.TestInfrastructure;

/// <summary>
/// 專門提供 HTTP Integration Testing 的
/// ASP.NET Core Test Host。
///
/// 功能：
/// 1. 載入正式 Program.cs 的應用程式設定
/// 2. 覆寫正式 AppDbContext 的 DI 註冊
/// 3. 使用 SQLite In-Memory Database
/// 4. 建立測試資料表
/// 5. 測試結束後釋放 Host 與資料庫連線
///
/// 注意：
/// 這個類別和 SqliteTestDatabase.cs 的用途不同。
///
/// SqliteTestDatabase：
/// 直接測試 EF Core / SQLite。
///
/// CustomWebApplicationFactory：
/// 測試 HTTP Request → ASP.NET Core → EF Core / SQLite。
/// </summary>
public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    // ==========================================
    // 1. 自訂 ASP.NET Core Test Host
    // ==========================================

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // 保留 WebApplicationFactory 原有設定。
        base.ConfigureWebHost(builder);


        // ======================================
        // 2. 覆寫 Configuration
        // ======================================

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                // 使用測試專用的設定覆寫正式設定。
                //
                // Database:AutoMigrate = false
                // 避免 Program.cs 啟動時執行 MigrateAsync。
                //
                // ConnectionStrings:DefaultConnection
                // 也改成記憶體資料庫。
                //
                // 這是額外的安全措施：
                // 即使不小心使用了原本的連線字串，
                // 也不應指向 Data/razor-commerce.db。
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Database:AutoMigrate"] = "false",

                        ["ConnectionStrings:DefaultConnection"] =
                            "Data Source=:memory:"
                    }
                );
            }
        );


        // ======================================
        // 3. 覆寫 DI Service Registrations
        // ======================================

        // ConfigureTestServices() 會在測試 Host
        // 建立 ServiceProvider 前修改 Service Collection。
        //
        // 因此可以替換 Program.cs 原本的
        // AppDbContext 設定。
        builder.ConfigureTestServices(services =>
        {
            // ----------------------------------
            // 3-1. 移除原本的 EF Core 設定
            // ----------------------------------

            // 移除舊的 AppDbContext Service 註冊。
            services.RemoveAll<AppDbContext>();

            // 移除舊的 DbContextOptions<AppDbContext>。
            //
            // Options 包含 EF Core Provider、
            // Connection String 等設定。
            services.RemoveAll<
                DbContextOptions<AppDbContext>>();

            // .NET / EF Core 10：
            //
            // AddDbContext() 也會註冊
            // IDbContextOptionsConfiguration<TContext>。
            //
            // 必須把原本的設定委派一起移除，
            // 才不會讓正式設定與測試設定同時套用。
            services.RemoveAll<
                IDbContextOptionsConfiguration<AppDbContext>>();


            // ----------------------------------
            // 3-2. 註冊測試用 SQLite Connection
            // ----------------------------------

            // 使用 Singleton：
            //
            // 在同一個 Test Host 生命週期內，
            // 所有 AppDbContext 都能使用
            // 同一個 SQLite In-Memory Database。
            //
            // 注意：
            // DbConnection 是 Singleton，
            // AppDbContext 仍然是 Scoped。
            services.AddSingleton<DbConnection>(_ =>
            {
                // 建立 SQLite 記憶體資料庫。
                //
                // Foreign Keys=True
                // 明確啟用 SQLite Foreign Key Constraints。
                var connection = new SqliteConnection(
                    "Data Source=:memory:;Foreign Keys=True"
                );

                // 保持連線開啟。
                //
                // 如果關閉這個 Connection，
                // 對應的 In-Memory Database 就會消失。
                connection.Open();

                // 連線交給 DI Container 管理。
                //
                // Test Host 被 Dispose 時，
                // DI Container 會釋放這個 Singleton。
                return connection;
            });


            // ----------------------------------
            // 3-3. 重新註冊測試版 AppDbContext
            // ----------------------------------

            // 這次使用 AddDbContext 的另一種 Overload。
            //
            // 第一個參數 serviceProvider：
            // 可以取得 DI Container 註冊的 Service。
            //
            // 第二個參數 options：
            // 設定 EF Core Database Provider。
            services.AddDbContext<AppDbContext>(
                (serviceProvider, options) =>
                {
                    // 從 DI Container 取得
                    // 前面註冊的 SQLite Connection。
                    var connection =
                        serviceProvider
                            .GetRequiredService<DbConnection>();

                    // 告訴 EF Core：
                    // AppDbContext 使用這個已開啟的
                    // SQLite In-Memory Connection。
                    options.UseSqlite(connection);
                }
            );
        });
    }


    // ==========================================
    // 4. 初始化測試資料庫
    // ==========================================

    /// <summary>
    /// 建立 Test Host 後，初始化 SQLite Schema。
    ///
    /// 因為使用 SQLite In-Memory，
    /// 每個新的 Factory 都需要建立資料表。
    ///
    /// 這裡不使用 Migrate()，
    /// 只使用 EnsureCreated()。
    /// </summary>
    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        // 建立並啟動 ASP.NET Core Test Host。
        var host = base.CreateHost(builder);

        try
        {
            // AppDbContext 預設是 Scoped Service。
            //
            // 目前不在 HTTP Request 裡，
            // 所以自行建立一個 DI Scope。
            using var scope =
                host.Services.CreateScope();

            // 這裡取得的 AppDbContext，
            // 應該已經是 ConfigureTestServices()
            // 重新註冊的測試版。
            var db =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            // 根據 EF Core Model 建立：
            //
            // Products
            // CartItems
            // Foreign Keys
            // Indexes
            //
            // 因為資料庫是全新的記憶體資料庫，
            // 所以不需要手動清理舊資料。
            // ======================================
            // 確認目前使用測試資料庫
            // ======================================

            // 取得 EF Core 實際使用的 Connection。

            // ======================================
            // 確認目前使用測試資料庫
            // ======================================

            // 取得 AppDbContext 實際使用的 DbConnection。
            var connection = db.Database.GetDbConnection();

            // 先確認 Provider 使用的是 Microsoft.Data.Sqlite。
            if (connection is not SqliteConnection sqliteConnection)
            {
                throw new InvalidOperationException(
                    "Test database must use SQLite.");
            }

            // 使用 SqliteConnectionStringBuilder 解析 Connection String。
            //
            // 注意：
            // sqliteConnection.DataSource 取得的是資料庫檔案路徑，
            // SQLite In-Memory 開啟後可能回傳空字串。
            //
            // 因此要使用 ConnectionStringBuilder.DataSource，
            // 才能確認當初指定的是不是 :memory:。
            var connectionSettings =
                new SqliteConnectionStringBuilder(
                    sqliteConnection.ConnectionString
                );

            // 確認資料庫來源是 :memory:。
            if (connectionSettings.DataSource != ":memory:")
            {
                throw new InvalidOperationException(
                    "Test database must use SQLite In-Memory.");
            }

            // 額外確認 Connection 仍然保持開啟。
            //
            // SQLite In-Memory Database 需要 Connection
            // 持續保持開啟，資料庫才會存在。
            if (sqliteConnection.State != System.Data.ConnectionState.Open)
            {
                throw new InvalidOperationException(
                    "SQLite In-Memory connection must remain open.");
            }

            // 安全檢查通過，才建立資料表。
            db.Database.EnsureCreated();

        }
        catch
        {
            // 如果資料庫初始化失敗，
            // 避免留下沒有被釋放的 Test Host。
            host.Dispose();

            // 保留原本的 Exception，
            // 讓 xUnit 顯示真正錯誤原因。
            throw;
        }

        // 回傳已建立 Schema 的 Test Host。
        return host;
    }
}
