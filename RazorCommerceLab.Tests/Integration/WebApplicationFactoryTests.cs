
using System.Net;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// 測試 ASP.NET Core HTTP Request / Response。
///
/// 現在改用 CustomWebApplicationFactory，
/// 因此 HTTP Test 不再直接使用正式資料庫設定。
/// </summary>
public class WebApplicationFactoryTests
{
    /// <summary>
    /// 存取不存在的路由時，
    /// ASP.NET Core 應回傳 HTTP 404。
    /// </summary>
    [Fact]
    public async Task GetUnknownRoute_ShouldReturnNotFound()
    {
        // ========================================
        // Arrange：建立測試用 ASP.NET Core Host
        // ========================================

        // 使用自訂的 Factory。
        //
        // Factory 會：
        // 1. 建立 ASP.NET Core Test Host
        // 2. 覆寫 AppDbContext DI
        // 3. 初始化 SQLite In-Memory DB
        using var factory =
            new CustomWebApplicationFactory();

        // 建立 HttpClient。
        using var client = factory.CreateClient();


        // ========================================
        // Act：發送 HTTP Request
        // ========================================

        // 發送 HTTP GET Request 到不存在的路由。
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
