# RazorCommerceLab

以 **ASP.NET Core Razor Pages + Minimal API + EF Core + SQLite** 實作的電商練習專案。

專案目標不是建立完整商用電商，而是用一個小型但完整的題目練習 ASP.NET Core 常見開發流程，包括 Razor Pages SSR、Minimal API、EF Core、匿名購物車、後台 CRUD、錯誤處理與 Docker Compose。

## Features

### 前台

- 首頁精選商品
- 商品列表、搜尋、分類篩選與分頁
- 商品詳細頁
- 商品圖片 fallback
- Cookie-based 匿名購物車
- 商品卡與詳細頁使用原生 `fetch` 加入購物車
- 購物車更新數量、移除商品、清空購物車

### 後台

- 獨立 Admin Layout
- 商品列表、搜尋、分類篩選與分頁
- 新增 / 編輯 / 刪除商品
- Razor Pages Form Handler
- DataAnnotations 表單驗證

### API

- 公開商品查詢 API
- DummyJSON 商品 Seed API
- 匿名購物車 API
- 後台商品 CRUD API

### 其他

- EF Core Migration
- SQLite
- 自訂 404 / 500 錯誤頁
- Docker multi-stage build
- Docker Compose
- Docker named volume 保存 SQLite DB

## Tech Stack

- C# / .NET 10
- ASP.NET Core Razor Pages
- ASP.NET Core Minimal API
- Entity Framework Core
- SQLite
- Razor / HTML / CSS
- Vanilla JavaScript / Fetch API
- Git
- Docker / Docker Compose

## Architecture Overview

```text
Browser
│
├─ Razor Pages
│  ├─ 首頁
│  ├─ 商品列表 / 詳細頁
│  ├─ 購物車
│  └─ Admin Pages
│
├─ JavaScript fetch
│  └─ Cart API
│
└─ Minimal API
   ├─ Product API
   ├─ Seed API
   ├─ Cart API
   └─ Admin Product API
          │
          ▼
       EF Core
          │
          ▼
        SQLite
```

Razor Pages 主要負責 HTML Server-Side Rendering；Minimal API 主要負責 JSON API。

## Project Structure

```text
razor-commerce-lab02/
├─ Dockerfile
├─ docker-compose.yml
├─ .dockerignore
├─ README.md
├─ NOTES.md
└─ RazorCommerceLab/
   ├─ Api/
   │  ├─ Dtos/
   │  ├─ ProductEndpoints.cs
   │  ├─ SeedEndpoints.cs
   │  ├─ CartEndpoints.cs
   │  └─ AdminProductEndpoints.cs
   ├─ Domain/
   ├─ Infrastructure/Persistence/
   ├─ Pages/
   │  ├─ Admin/
   │  ├─ Cart/
   │  ├─ Products/
   │  ├─ Shared/
   │  └─ StatusCode/
   ├─ Services/
   ├─ Migrations/
   ├─ Data/
   └─ wwwroot/
```

# Local Development

## Requirements

- .NET 10 SDK
- Git
- 若使用 Docker：Docker Engine / Docker Desktop + Docker Compose

## 1. Clone

```bash
git clone <repository-url>
cd razor-commerce-lab02/RazorCommerceLab
```

## 2. Restore

```bash
dotnet restore
```

## 3. Apply EF Core Migration

```bash
dotnet ef database update
```

SQLite database：

```text
Data/razor-commerce.db
```

## 4. Start Application

```bash
dotnet run
```

本機開發預設：

```text
http://localhost:5024
```

> 實際 port 以 `Properties/launchSettings.json` 為準。

## 5. Import Sample Products

```bash
curl -X POST http://localhost:5024/api/seed/products
```

測試：

```bash
curl http://localhost:5024/api/products
```

# Docker Compose

從 repo root 執行：

```bash
docker compose up -d --build
```

網站：

```text
http://localhost:5033
```

查看狀態：

```bash
docker compose ps
```

查看 log：

```bash
docker compose logs -f
```

第一次啟動後可匯入測試商品：

```bash
curl -X POST http://localhost:5033/api/seed/products
```

停止：

```bash
docker compose down
```

`docker compose down` 會保留 SQLite named volume。

若連資料一起刪除：

```bash
docker compose down -v
```

## Main Pages

| Route | Description |
|---|---|
| `/` | 首頁 |
| `/Products` | 商品列表 |
| `/Products/Details/{id}` | 商品詳細頁 |
| `/Cart` | 匿名購物車 |
| `/Admin` | 後台首頁 |
| `/Admin/Products` | 後台商品列表 |
| `/Admin/Products/Create` | 新增商品 |
| `/Admin/Products/Edit/{id}` | 編輯商品 |
| `/Admin/Products/Delete/{id}` | 刪除商品確認 |

# API

## Products

```http
GET /api/products
GET /api/products/{id}
```

列表支援：

```text
keyword
category
page
pageSize
```

例如：

```text
/api/products?keyword=phone&category=smartphones&page=1&pageSize=12
```

## Seed

```http
POST /api/seed/products
```

從 DummyJSON 匯入測試商品，使用 `ExternalId` 避免重複匯入。

## Cart

```http
GET    /api/cart
POST   /api/cart/items
PUT    /api/cart/items/{productId}
DELETE /api/cart/items/{productId}
DELETE /api/cart
```

匿名購物車 Cookie：

```text
razor_commerce_cart
```

Cookie 只保存匿名購物車識別碼 `CartKey`，實際資料存放在 SQLite `CartItems` table。

## Admin Products

```http
GET    /api/admin/products/{id}
POST   /api/admin/products
PUT    /api/admin/products/{id}
DELETE /api/admin/products/{id}
```

目前專案沒有 Authentication / Authorization，因此 `/Admin` 與 `/api/admin/*` 只是學習用後台結構，不代表正式環境的權限保護設計。

# Validation

使用 DataAnnotations：

```csharp
[Required]
[StringLength(...)]
[Range(...)]
[Url]
```

Razor Pages 表單流程：

```text
HTML Form
↓
Model Binding
↓
[BindProperty]
↓
DataAnnotations
↓
ModelState.IsValid
↓
EF Core
```

# Anonymous Cart Design

```text
Browser
│
│ Cookie: razor_commerce_cart=<CartKey>
│
▼
ASP.NET Core
│
▼
CartItems
├─ CartKey
├─ ProductId
└─ Quantity
```

Cookie 使用 `HttpOnly`，JavaScript 不直接讀取 CartKey。

商品加入購物車：

```text
Razor HTML
↓
JavaScript fetch
↓
POST /api/cart/items
↓
Cookie CartKey
↓
EF Core
↓
SQLite
```

`/Cart` 則刻意使用 Razor Page Handler：

```text
POST
↓
OnPostUpdateAsync / Remove / Clear
↓
EF Core
↓
TempData
↓
RedirectToPage
↓
GET
```

# Error Handling

404：

```text
UseStatusCodePagesWithReExecute
↓
/StatusCode/404
```

500：

```text
Unhandled Exception
↓
UseExceptionHandler
↓
/Error
```

錯誤頁不直接公開 Exception stack trace 或內部敏感資訊。

# Docker Design

Dockerfile 使用 multi-stage build：

```text
mcr.microsoft.com/dotnet/sdk:10.0
↓
dotnet restore
↓
dotnet publish

mcr.microsoft.com/dotnet/aspnet:10.0
↓
dotnet RazorCommerceLab.dll
```

Docker Compose：

```text
Host :5033
↓
Container :8080
↓
ASP.NET Core
↓
/app/Data/razor-commerce.db
↓
Docker named volume
```

# What I Practiced

- ASP.NET Core request pipeline
- Razor Pages PageModel
- `OnGetAsync` / `OnPostAsync` / Named Handler
- Tag Helpers / Partial Views / `_ViewStart` / Layout
- Model Binding / `[BindProperty]`
- DataAnnotations / `ModelState.IsValid`
- TempData / PRG（Post / Redirect / Get）
- Minimal API / DTO
- EF Core DbContext / LINQ / IQueryable / AsNoTracking
- Migration / Foreign Key
- SQLite
- Cookie / Fetch API
- Docker multi-stage build / Compose / volume

# Scope / Limitations

這是學習與作品展示專案，因此刻意沒有加入：

- Authentication / Authorization
- User account
- Order / Checkout / Payment
- PostgreSQL
- Redis / Message Queue
- Nginx
- React / Vue
- Bootstrap / Tailwind CSS

目標是保持範圍小而完整，專注練習 ASP.NET Core Razor Pages、Minimal API、EF Core 與 Docker 的基本整合。
