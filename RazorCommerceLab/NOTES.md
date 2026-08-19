# RazorCommerceLab Learning Notes

這份文件整理 RazorCommerceLab 從 Razor Pages、Minimal API、EF Core、SQLite、購物車、後台 CRUD、錯誤處理到 Docker Compose 的主要學習重點。

用途：

- 之後快速複習
- 面試前回顧 ASP.NET Core
- 不需要重新翻很長的對話紀錄

---

# 1. 專案定位

```text
ASP.NET Core Razor Pages
Minimal API
EF Core
SQLite
Vanilla JavaScript
Docker Compose
```

刻意沒有加入 Auth、Order、Payment、PostgreSQL、Redis、React、Vue、Bootstrap、Tailwind、Clean Architecture 等額外範圍。

目標是把 ASP.NET Core 一條主線完整做完。

# 2. ASP.NET Core 啟動流程

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(...);

var app = builder.Build();

app.UseRouting();
app.MapRazorPages();
app.MapProductEndpoints();

app.Run();
```

順序：

```text
CreateBuilder
↓
註冊 Services / DI / Configuration
↓
Build
↓
設定 Middleware / Endpoint
↓
Run
```

重要：service registration 必須在 `builder.Build()` 前完成。

# 3. Razor Pages

通常一頁：

```text
Index.cshtml
Index.cshtml.cs
```

`.cshtml`：HTML + Razor。

`.cshtml.cs`：PageModel、Handler、EF Core query、Form processing。

## OnGet

```csharp
public async Task OnGetAsync()
```

```text
GET Request
↓
OnGetAsync
↓
Render Razor Page
```

## POST Handler

```html
<form method="post">
```

對應：

```csharp
OnPostAsync()
```

Named Handler：

```cshtml
<form method="post" asp-page-handler="Update">
```

對應：

```csharp
OnPostUpdateAsync()
```

# 4. Razor C# Scope

曾遇到：

```text
Unexpected "{" after "@" character
```

錯誤：

```cshtml
@foreach (var item in Model.Items)
{
    @{
        var imageUrl = "...";
    }
}
```

在 `@foreach {}` 裡已經是 C# context，不需要再 `@{}`。

正確：

```cshtml
@foreach (var item in Model.Items)
{
    var imageUrl = "...";
    <img src="@imageUrl" />
}
```

如果出現：

```text
imageUrl does not exist in current context
```

通常代表使用 `imageUrl` 的 HTML 已經跑到宣告它的 `{}` scope 外。

# 5. Layout / _ViewStart

全站：

```text
Pages/_ViewStart.cshtml
→ _Layout
```

後台：

```text
Pages/Admin/_ViewStart.cshtml
→ _AdminLayout
```

因此 Admin 子目錄可以自動使用不同 Layout。

# 6. EF Core + SQLite

主要 Entity：

```text
Product
CartItem
```

`AppDbContext` 透過 DI 注入 Razor Page / Minimal API，不自己 `new AppDbContext()`。

# 7. Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

```text
Entity / Fluent API
↓
EF Core Model
↓
Migration
↓
Database Schema
```

`migrations add` 產生 migration code；`database update` 才真正套用 DB。

# 8. CartItem Relationship

```text
CartItem.ProductId
→ Foreign Key

CartItem.Product
→ Navigation Property
```

Fluent API：

```csharp
entity.HasOne(item => item.Product)
    .WithMany()
    .HasForeignKey(item => item.ProductId)
    .OnDelete(DeleteBehavior.Cascade);
```

# 9. AsNoTracking

只讀：

```csharp
_db.Products.AsNoTracking()
```

適合列表、詳細頁 GET、API GET。

要修改 tracked entity 時不要使用 `AsNoTracking()`。

# 10. Tracked Entity Update

```csharp
var product = await db.Products.FirstAsync(...);
product.Title = "New";
await db.SaveChangesAsync();
```

通常不需要再：

```csharp
db.Products.Update(product);
```

因為它本來就是目前 DbContext 查出的 tracked entity。

# 11. IQueryable

```csharp
var query = _db.Products.AsNoTracking().AsQueryable();

if (...) query = query.Where(...);
```

在 `CountAsync()`、`ToListAsync()`、`FirstOrDefaultAsync()` 等 terminal operation 前，通常只是逐步組 query。

# 12. Pagination

```csharp
.Skip((page - 1) * pageSize)
.Take(pageSize)
```

`CountAsync()` 要在 `Skip/Take` 前，才能得到符合條件的總筆數。

# 13. Query String Binding

```csharp
[BindProperty(SupportsGet = true)]
public string? Keyword { get; set; }
```

```text
?Keyword=phone
↓
Keyword = "phone"
```

# 14. Minimal API `[AsParameters]` 陷阱

曾遇到：

```text
Required parameter "int Page" was not provided from query string
```

原本：

```csharp
public int Page { get; init; } = 1;
```

改為：

```csharp
public int? Page { get; init; }
public int? PageSize { get; init; }
```

handler：

```csharp
var page = query.Page ?? 1;
var pageSize = query.PageSize ?? 12;
```

重點：property initializer 不代表 Minimal API binding 一定會把 non-nullable simple parameter 視為 optional。

# 15. Minimal API / MapGroup

例如：

```csharp
var group = app.MapGroup("/api/cart");
group.MapGet("/", ...);
group.MapPost("/items", ...);
```

可以把 route prefix 集中管理。

# 16. DTO

不要把 DB Entity 直接當所有 API contract。

```text
Product
→ DB Entity

ProductDto
→ 公開 API Response

AdminProductWriteRequest
→ Admin API Request
```

DTO 可以控制哪些欄位公開、哪些欄位可修改，並集中 Validation。

# 17. DataAnnotations

常用：

```csharp
[Required]
[StringLength(...)]
[Range(...)]
[Url]
```

Minimal API：

```text
JSON
↓
DTO
↓
Validation
↓
Handler
```

Razor：

```text
Form
↓
Model Binding
↓
Input Model
↓
DataAnnotations
↓
ModelState
```

# 18. ModelState.IsValid

```csharp
if (!ModelState.IsValid)
{
    return Page();
}
```

Validation 失敗用 `return Page()`，讓本次 POST 的輸入和 validation error 保留。

成功才 `RedirectToPage()`。

# 19. `[BindProperty]`

```csharp
[BindProperty]
public ProductFormInput Input { get; set; } = new();
```

表單中的 `Input.Title`、`Input.Price` 等會綁定到 PageModel 的 `Input`。

# 20. Partial Form

Create / Edit 共用：

```text
_ProductForm.cshtml
```

```cshtml
<partial name="_ProductForm" for="Input" />
```

避免兩頁複製整份表單。

# 21. Input Model vs Entity

不要直接：

```csharp
[BindProperty]
public Product Product { get; set; }
```

因為 Entity 還有 `Id`、`ExternalId`、`ImageUrlsJson`、`CreatedAt` 等不應由表單任意修改的資料。

應使用：

```text
ProductFormInput
↓
ProductFormHelper
↓
Product
```

# 22. DummyJSON Seed

```text
POST /api/seed/products
↓
DummyJsonProductClient
↓
DummyJSON
↓
External DTO
↓
Product
↓
SQLite
```

用 `ExternalId` 保存外部原始 ID，並用 existing IDs + HashSet 避免重複匯入；DB unique index 保留最後的完整性保護。

# 23. Image Fallback

不把 placeholder URL 寫死到 DB。

```text
ThumbnailUrl 有值
↓
使用原 URL

ThumbnailUrl 空
↓
Picsum fixed seed

圖片載入失敗
↓
本機 placeholder.svg
```

# 24. Anonymous Cart

Cookie：

```text
razor_commerce_cart
```

只放 CartKey。

真正資料：

```text
CartItems
├─ CartKey
├─ ProductId
└─ Quantity
```

# 25. HttpOnly / fetch

Cookie 設 `HttpOnly = true`，JavaScript 不直接讀 CartKey。

fetch：

```javascript
fetch("/api/cart/items", {
    method: "POST",
    headers: {
        "Content-Type": "application/json"
    },
    credentials: "same-origin",
    body: JSON.stringify({
        productId,
        quantity: 1
    })
});
```

Browser 自己處理同源 Cookie。

# 26. Client Validation ≠ Server Validation

JavaScript 驗證主要改善 UX。

使用者仍可用 curl、Postman、DevTools 自己送 request，所以真正的規則仍要在 server 驗證。

# 27. Razor Cart Page / Named Handler

```text
asp-page-handler="Update"
→ OnPostUpdateAsync

asp-page-handler="Remove"
→ OnPostRemoveAsync

asp-page-handler="Clear"
→ OnPostClearAsync
```

# 28. PRG / TempData

成功 POST：

```text
POST
↓
修改 DB
↓
TempData
↓
RedirectToPage
↓
GET
```

好處是 F5 不會重新提交上一個 POST。

`TempData` 適合顯示新增成功、修改成功、刪除成功等一次性訊息。

# 29. Admin Product List

後台 Razor Page 直接注入 `AppDbContext` 查 SQLite，不會再繞 HTTP 呼叫自己同一個 ASP.NET Core process 的 `/api/admin/products`。

這個專案同時保留 Admin JSON API 與 Razor CRUD，是為了練兩種 endpoint 模式，不代表實務上每個畫面都一定要重複寫兩套 CRUD。

# 30. Delete：GET 確認、POST 執行

```text
GET /Delete/5
↓
顯示確認頁

POST /Delete/5
↓
真正 Remove + SaveChanges
```

不要用 GET 做 destructive state change。

# 31. 404 Error

```csharp
app.UseStatusCodePagesWithReExecute(
    "/StatusCode/{0}");
```

```text
GET /not-exist
↓
404
↓
Server 內部重新執行 /StatusCode/404
↓
自訂 HTML
```

瀏覽器網址仍保持原 URL。

# 32. ReExecute ≠ Redirect

Redirect：

```text
Browser GET A
↓
302
↓
Browser GET B
```

ReExecute：

```text
Browser GET A
↓
Server 內部換 Path
↓
Pipeline 再跑
↓
Browser 仍看到 A
```

# 33. 500 Exception Handler

```csharp
app.UseExceptionHandler("/Error");
```

```text
Unhandled Exception
↓
ExceptionHandler Middleware
↓
/Error
↓
HTTP 500
```

不要把 StackTrace、DB 資訊、server path 直接公開給使用者。可以顯示 Request ID 方便 log trace。

# 34. API Error vs HTML Error

Minimal API 如果自己回：

```csharp
Results.NotFound(new { message = "..." })
```

應保留 JSON response。

```text
Razor Page error
→ HTML

Minimal API error
→ JSON
```

# 35. Docker Multi-stage Build

```text
SDK image
↓
restore
↓
publish
↓
/app/publish

ASP.NET runtime image
↓
copy publish output
↓
dotnet RazorCommerceLab.dll
```

正式 runtime image 不需要完整 SDK。

# 36. dotnet run vs DLL

開發：

```bash
dotnet run
```

publish 後：

```bash
dotnet RazorCommerceLab.dll
```

Docker：

```dockerfile
ENTRYPOINT ["dotnet", "RazorCommerceLab.dll"]
```

# 37. Docker Port

```yaml
ports:
  - "5033:8080"
```

```text
Host 5033
↓
Container 8080
```

主機瀏覽：

```text
http://localhost:5033
```

# 38. SQLite Docker Volume

```yaml
volumes:
  - razor-commerce-data:/app/Data
```

```text
ASP.NET Core
↓
/app/Data/razor-commerce.db
↓
Docker named volume
```

container 重建後資料仍可存在。

# 39. docker compose down

```bash
docker compose down
```

刪 container / network，但保留 named volume。

```bash
docker compose down -v
```

連 volume 一起刪除，所以 SQLite 資料也會清掉。

# 40. Docker Fresh DB / AutoMigrate

Compose：

```yaml
Database__AutoMigrate: "true"
```

環境變數：

```text
Database__AutoMigrate
↓
Database:AutoMigrate
```

Program 依設定建立 scope 並執行：

```csharp
await db.Database.MigrateAsync();
```

這讓新的 Docker named volume 第一次啟動時可以建立 schema。

本機未設定 AutoMigrate 時仍可手動：

```bash
dotnet ef database update
```

# 41. ASP.NET Core Configuration

```text
ConnectionStrings__DefaultConnection
↓
ConnectionStrings:DefaultConnection
```

雙底線 `__` 用來表達 configuration hierarchy。

Docker 可以用 environment variable 覆蓋 `appsettings.json` 的 Connection String。

# 42. 完整 Request Flow

前台：

```text
Browser
↓
Razor Page
↓
PageModel
↓
EF Core
↓
SQLite
↓
HTML
```

Product API：

```text
Client
↓
Minimal API
↓
EF Core
↓
SQLite
↓
JSON
```

加入購物車：

```text
Razor HTML
↓
JavaScript fetch
↓
Cart API
↓
Cookie CartKey
↓
EF Core
↓
SQLite
↓
JSON
```

Cart Razor Page：

```text
Form
↓
Page Handler
↓
EF Core
↓
TempData
↓
Redirect
↓
GET
↓
HTML
```

Docker：

```text
localhost:5033
↓
Docker
↓
ASP.NET Core :8080
↓
EF Core
↓
SQLite volume
```

# 43. 面試 / 複習時至少要能說明

1. Razor Pages 和 Minimal API 的差異。
2. PageModel 和 `.cshtml` 的分工。
3. `OnGet`、`OnPost`、Named Handler 如何運作。
4. `[BindProperty]` 與 Model Binding。
5. DataAnnotations 與 `ModelState.IsValid`。
6. EF Core DbContext 如何透過 DI 注入。
7. `AsNoTracking()` 什麼時候使用。
8. tracked entity 為什麼不一定要 `Update()`。
9. Migration 與 `database update` 的差異。
10. `IQueryable` 如何逐步組 query。
11. `Skip/Take` pagination。
12. DTO 和 Entity 為什麼分開。
13. Cookie-based anonymous cart 如何運作。
14. `HttpOnly` 與 `credentials: same-origin`。
15. Server validation 為什麼不能被 client validation 取代。
16. PRG 與 TempData。
17. `_ViewStart` 如何讓 Admin 使用不同 Layout。
18. 404 StatusCodePages 與 500 ExceptionHandler 的差異。
19. Docker multi-stage build 的目的。
20. `5033:8080` 的意思。
21. SQLite 為什麼需要 Docker volume。
22. Docker environment variable 如何覆蓋 appsettings。
23. `dotnet run` 與 publish 後執行 DLL 的差異。

# 44. 最後總結

這個專案完整走過：

```text
建立專案
↓
Razor Pages
↓
EF Core
↓
Migration
↓
Minimal API
↓
Validation
↓
External API Seed
↓
商品前台
↓
Cookie Cart
↓
Fetch
↓
Razor Form Handler
↓
Admin Layout
↓
Admin CRUD
↓
Error Handling
↓
Docker Compose
```

之後再做下一個 ASP.NET Core 專案，建議不要照這份文件逐字複製；只在忘記某個觀念或流程時回來查。
