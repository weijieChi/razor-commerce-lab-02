var builder = WebApplication.CreateBuilder(args);

// ==========================================
// Service registration
// ==========================================

// 加入 Razor Pages 相關服務。
// 之後 Pages/*.cshtml 與 PageModel 才能由 ASP.NET Core 處理。
builder.Services.AddRazorPages();

var app = builder.Build();


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
