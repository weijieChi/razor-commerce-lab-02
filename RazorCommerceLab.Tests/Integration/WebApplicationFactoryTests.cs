
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// ASP.NET Core WebApplicationFactory 基礎測試。
///
/// 本測試目標：
///
/// 1. 建立 ASP.NET Core Test Host
/// 2. 使用 HttpClient 發送 HTTP Request
/// 3. 驗證 Routing 與 404 回應
///
/// 這裡還沒有實作 Test Database DI Override，
/// 所以暫時不呼叫會存取資料庫的 API。
/// </summary>
public class WebApplicationFactoryTests
{
    /// <summary>
    /// 當使用者請求不存在的網址時，
    /// ASP.NET Core 應回傳 404 Not Found。
    /// </summary>
    [Fact]
    public async Task GetUnknownRoute_ShouldReturnNotFound()
    {
        // ========================================
        // Arrange：建立測試用 ASP.NET Core Host
        // ========================================

        // WebApplicationFactory<Program>：
        //
        // Program 指向正式專案的 Entry Point。
        //
        // Factory 會利用正式專案的啟動流程，
        // 建立一個測試用 ASP.NET Core Application。
        //
        // 預設使用 TestServer，
        // 不需要啟動外部 Kestrel 監聽 Port。
        //
        // WithWebHostBuilder()：
        // 允許對這次測試的 Host 做額外設定。
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // 加入測試專用的 Configuration。
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        // 明確停用啟動時自動 Migration。
                        //
                        // 避免測試啟動 Host 時，
                        // 執行 Program.cs 的 AutoMigrate。
                        //
                        // 這裡只覆寫 Configuration，
                        // 尚未取代正式 AppDbContext 的 DI。
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Database:AutoMigrate"] = "false"
                            }
                        );
                    }
                );
            });

        // 建立 HttpClient。
        //
        // 這個 Client 會將 Request
        // 送到 Factory 所管理的 TestServer。
        //
        // 不需要：
        //
        // dotnet run
        // localhost:5024
        // localhost:5033
        //
        // CreateClient() 也會觸發
        // Test Host 的建立與啟動。
        using var client = factory.CreateClient();


        // ========================================
        // Act：發送 HTTP Request
        // ========================================

        // 發送 GET Request 到不存在的路由。
        //
        // 預期 ASP.NET Core：
        //
        // Routing 找不到 Endpoint
        //     ↓
        // 回傳 404
        //     ↓
        // StatusCodePages Middleware
        //     ↓
        // 重新執行 /StatusCode/404
        //
        // 此路由不需要連接資料庫。
        using var response = await client.GetAsync(
            "/this-route-does-not-exist"
        );


        // ========================================
        // Assert：驗證 HTTP Response
        // ========================================

        // HttpStatusCode.NotFound = HTTP 404。
        //
        // 如果實際回傳其他 Status Code，
        // 例如 200 或 500，
        // xUnit 就會判定這個測試失敗。
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );
    }
}
