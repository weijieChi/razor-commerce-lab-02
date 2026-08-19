using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Api;
using RazorCommerceLab.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// Razor Pages
// ==========================================

// 加入 Razor Pages 相關服務。
// 之後 Pages/*.cshtml 與 PageModel 才能由 ASP.NET Core 處理。
builder.Services.AddRazorPages();


// ==========================================
// Minimal API Validation
// ==========================================

/*
    啟用 .NET 10 Minimal API 內建 Validation。

    Endpoint parameter 綁定完成後，
    ASP.NET Core 會檢查：

    [Required]
    [Range]
    [StringLength]
    ...

    如果驗證失敗：

    Request
       ↓
    Binding
       ↓
    Validation ❌
       ↓
    HTTP 400

    Endpoint handler 不會被執行。
*/
// AddValidation() 是 .NET 10 Minimal API 啟用這套 validation pipeline 的入口。
builder.Services.AddValidation();


// ==========================================
// EF Core / SQLite
// ==========================================

/*
    從 appsettings.json 讀取：

    ConnectionStrings:
        DefaultConnection

    如果沒有設定，直接丟出明確錯誤，
    比讓 EF Core 到後面才因為 null 出錯容易除錯。
*/

/*
    GetConnectionString("DefaultConnection")

    會去 Configuration 找：

    ConnectionStrings:DefaultConnection

    也就是 appsettings.json：

    {
      "ConnectionStrings": {
        "DefaultConnection":
          "Data Source=Data/razor-commerce.db"
      }
    }
*/
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
/*
    將 AppDbContext 註冊進 ASP.NET Core DI container。

    這個註冊非常重要。

    AppDbContext 的 constructor 需要：

    DbContextOptions<AppDbContext>

    AddDbContext() 就會替 DI 準備這個 options，
    並告訴 EF Core要使用 SQLite。

    之後 PageModel 或 Minimal API 如果需要：

    AppDbContext db

    ASP.NET Core 就會自動建立並傳進去。

    不需要自己：

    new AppDbContext(...)
*/
builder.Services.AddDbContext<AppDbContext>(options =>
{

    /*
        告訴 EF Core：

        這個 AppDbContext 使用 SQLite provider。
    */
    options.UseSqlite(connectionString);
});

// ==========================================
// DummyJSON HttpClient
// ==========================================

/*
    註冊 DummyJsonProductClient 為 Typed HttpClient。

    ASP.NET Core 會：

    1. 建立 HttpClient
    2. 套用這裡的設定
    3. 建立 DummyJsonProductClient
    4. 把 HttpClient 注入 constructor
*/
builder.Services.AddHttpClient<DummyJsonProductClient>(client =>
{
    /*
        之後 DummyJsonProductClient 裡可以只寫：

        products?limit=100&skip=0

        不需要每次重複：

        https://dummyjson.com/
    */
    client.BaseAddress =
        new Uri("https://dummyjson.com/");

    /*
        外部服務一直沒有回應時，
        不希望 request 永遠卡住。
    */
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();


// ==========================================
// Database migration
// ==========================================

/*
    一般本機開發：

    Database:AutoMigrate
    預設不存在 / false

    所以仍然自己執行：

    dotnet ef database update


    Docker Compose：

    Database__AutoMigrate=true

    ↓ ASP.NET Core Configuration

    Database:AutoMigrate=true

    ↓

    啟動 container 時自動套用
    已經存在的 EF Core migrations。


    目的：

    新的 Docker named volume 一開始沒有 SQLite DB，
    Docker 第一次啟動時可以自動建立 schema。
*/
if (app.Configuration.GetValue<bool>(
        "Database:AutoMigrate"))
{
    /*
        AppDbContext 是 Scoped service。

        Program.cs 目前不在 HTTP Request scope 中，
        所以自己建立一個暫時的 DI scope。
    */
    using var scope =
        app.Services.CreateScope();


    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();


    /*
        套用 Migrations/ 裡尚未執行的 migration。

        如果 DB 不存在：
        → 建立 DB
        → 建立 schema

        如果 migration 已經套用：
        → 不會重複建立相同 schema
    */
    await db.Database.MigrateAsync();
}


// ==========================================
// Error handling
// ==========================================

/*
    處理沒有被程式 catch 的 Exception。

    例如：

    Razor Page
        ↓
    EF Core query
        ↓
    發生 Exception
        ↓
    沒有 catch
        ↓
    UseExceptionHandler
        ↓
    /Error
        ↓
    500 Error Page
*/
app.UseExceptionHandler(
    "/Error");


/*
    處理 HTTP Status Code。

    最主要練習：

    404 Not Found

    例如：

    GET /abcdef
        ↓
    404
        ↓
    Server 內部重新執行：

    /StatusCode/404

    注意：
    這不是 Browser Redirect。
*/
app.UseStatusCodePagesWithReExecute(
    "/StatusCode/{0}");


// ==========================================
// HTTP middleware pipeline
// ==========================================

// 這個練習專案目前刻意只使用 HTTP。
// 因此不要加入：
//
// app.UseHttpsRedirection();
//
// 如果啟用它，HTTP request 會嘗試被重新導向 HTTPS。
// 但目前我們沒有啟動 HTTPS port，因此會產生：
//
// Failed to determine the https port for redirect.
//
// 未來正式部署需要 HTTPS 時，
// 可以由 Web Server / Reverse Proxy / Cloud 平台處理 TLS。


// 啟用 ASP.NET Core Routing。
// Razor Pages 與後面的 Minimal API 都會依靠 Routing
// 決定 request 應該交給哪個 endpoint。

// ==========================================
// Routing
// ==========================================
app.UseRouting();


// ==========================================
// Static assets
// ==========================================

// .NET 目前 Razor template 使用 MapStaticAssets()
// 提供 wwwroot 底下的靜態檔案，例如：
//
// wwwroot/css/site.css
//        ↓
// /css/site.css
//
// wwwroot/js/site.js
//        ↓
// /js/site.js
app.MapStaticAssets();


// ==========================================
// Razor Pages
// ==========================================

// 把 Pages/ 底下的 Razor Pages 註冊成 HTTP endpoints。
//
// 例如：
//
// Pages/Index.cshtml
//        ↓
// GET /
app.MapRazorPages()
   .WithStaticAssets();

// ==========================================
// Minimal API
// ==========================================

// 註冊商品 API：
//
// GET /api/products
// GET /api/products/{id}
app.MapProductEndpoints();

// ==========================================
// Seed API
// ==========================================

// POST /api/seed/products
app.MapSeedEndpoints();

// ==========================================
// Cart API
// ==========================================

// GET    /api/cart
// POST   /api/cart/items
// PUT    /api/cart/items/{productId}
// DELETE /api/cart/items/{productId}
// DELETE /api/cart
app.MapCartEndpoints();

// ==========================================
// Admin Product API
// ==========================================

// GET    /api/admin/products/{id}
// POST   /api/admin/products
// PUT    /api/admin/products/{id}
// DELETE /api/admin/products/{id}
app.MapAdminProductEndpoints();

// ==========================================
// TEMP：Step 13 測試 500
// 測試完成後刪除。
// ==========================================

// app.MapGet(
//     "/__test-error",
//     () =>
//     {
//         throw new InvalidOperationException(
//             "Step 13 test exception.");
//     });


// 啟動 ASP.NET Core Web Application。
app.Run();

// 注意我們這次連：
//
// app.UseAuthorization();
//
// 也先拿掉。
//
// 原因很單純：
//
// 目前沒有登入
// 目前沒有 Authorization
// 目前沒有 [Authorize]
//
// 所以暫時沒有必要。
//
// 未來這個 RazorCommerceLab 本來也沒有規劃登入 / 權限功能。
