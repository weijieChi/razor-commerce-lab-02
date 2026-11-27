
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;
using RazorCommerceLab.Pages.Admin.Products;
using RazorCommerceLab.Tests.TestInfrastructure;
using Xunit;

namespace RazorCommerceLab.Tests.Integration;

/// <summary>
/// Razor Page Handler Integration Tests。
///
/// 不啟動 WebApplicationFactory，
/// 不透過 HttpClient 發送 HTTP Request。
///
/// 直接建立 PageModel，呼叫 Handler Method，
/// 並驗證 IActionResult、ModelState、TempData
/// 以及 SQLite 資料庫狀態。
/// </summary>
public class RazorPageHandlerTests
{
    // ==========================================
    // Test Input Factory
    // ==========================================

    /// <summary>
    /// 建立合法的商品表單資料。
    ///
    /// 每次呼叫都建立新的 Input Object，
    /// 避免不同測試共用可修改的物件。
    /// </summary>
    private static ProductFormInput CreateValidInput()
    {
        return new ProductFormInput
        {
            Title = "Mechanical Keyboard",
            Description = "Test keyboard",

            Price = 1500m,
            DiscountPercentage = 10m,
            Rating = 4.5m,
            Stock = 10,

            Brand = "Test Brand",
            Category = "electronics",

            ThumbnailUrl =
                "https://example.com/keyboard.jpg",

            IsFeatured = true
        };
    }


    // ==========================================
    // TempData Test Stub
    // ==========================================

    /// <summary>
    /// 提供測試專用的 TempData Provider。
    ///
    /// 正式環境可能透過 Cookie 或 Session
    /// 保存跨 Request 的 TempData。
    ///
    /// 本次只驗證 PageModel 是否正確設定 TempData，
    /// 不需要實際操作 Cookie 或 Session。
    /// </summary>
    private sealed class TestTempDataProvider
        : ITempDataProvider
    {
        /// <summary>
        /// 模擬讀取 TempData。
        ///
        /// 每次初始化時都回傳空的 Dictionary。
        /// </summary>
        public IDictionary<string, object> LoadTempData(
            HttpContext context)
        {
            return new Dictionary<string, object>();
        }


        /// <summary>
        /// 模擬儲存 TempData。
        ///
        /// 本次不測試跨 Request 的資料保存，
        /// 所以不需要實作實際的儲存行為。
        /// </summary>
        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
            // Intentionally empty.
        }
    }


    // ==========================================
    // PageModel Factory
    // ==========================================

    /// <summary>
    /// 建立測試專用的 CreateModel。
    ///
    /// 正式環境中的 PageContext、ViewData、
    /// ModelState 和 TempData，
    /// 通常由 ASP.NET Core 自動建立。
    ///
    /// 直接測試 PageModel 時，
    /// 必須自行準備所需的環境。
    /// </summary>
    private static CreateModel CreatePageModel(
        AppDbContext db)
    {
        // 建立模擬的 HTTP Context。
        //
        // 這裡沒有實際的 HTTP Request，
        // 也沒有啟動 Web Server。
        var httpContext =
            new DefaultHttpContext();


        // 保存 Model Binding 和 Validation 的結果。
        var modelState =
            new ModelStateDictionary();


        // 建立 MVC ActionContext。
        //
        // 包含 HttpContext、RouteData、
        // PageActionDescriptor 和 ModelState。
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            modelState
        );


        // 建立 ViewData。
        //
        // 使用相同的 ModelStateDictionary，
        // 讓 ViewData 和 PageContext
        // 共用一致的表單驗證狀態。
        var viewData = new ViewDataDictionary(
            new EmptyModelMetadataProvider(),
            modelState
        );


        // 建立 Razor Pages 專用的 Context。
        var pageContext = new PageContext(
            actionContext
        )
        {
            ViewData = viewData
        };


        // 建立 TempDataDictionary。
        //
        // 使用真實的 TempDataDictionary，
        // 但將底層儲存 Provider 替換成測試 Stub。
        var tempData = new TempDataDictionary(
            httpContext,
            new TestTempDataProvider()
        );


        // 手動進行 Constructor Injection。
        //
        // 不需要 DI Container，
        // 直接將測試用 AppDbContext 傳入。
        return new CreateModel(db)
        {
            PageContext = pageContext,
            TempData = tempData
        };
    }


    // ==========================================
    // Test 1：Valid ModelState
    // ==========================================

    /// <summary>
    /// 驗證合法表單提交後：
    ///
    /// 1. 商品寫入資料庫
    /// 2. 回傳 RedirectToPageResult
    /// 3. Redirect 指向商品列表
    /// 4. TempData 包含成功訊息
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ShouldCreateProductAndRedirect_WhenModelStateIsValid()
    {
        // ========================================
        // Arrange
        // ========================================

        // 沿用 Step 16-6 建立的測試資料庫。
        using var database =
            new SqliteTestDatabase();

        using var db = database.CreateContext();


        // 建立 PageModel。
        var pageModel = CreatePageModel(db);


        // 模擬 Model Binding 完成後的 Input。
        pageModel.Input = CreateValidInput();


        // 沒有加入 ModelState Error，
        // 代表模擬表單驗證通過的狀態。
        Assert.True(pageModel.ModelState.IsValid);

        Assert.Equal(
            0,
            await db.Products.CountAsync()
        );


        // ========================================
        // Act
        // ========================================

        // 直接呼叫 Razor Page Handler。
        //
        // CancellationToken.None 表示
        // 本次操作不需要取消控制。
        var result = await pageModel.OnPostAsync(
            CancellationToken.None
        );


        // ========================================
        // Assert：Handler Result
        // ========================================

        // 確認 Handler 回傳的具體型別。
        //
        // Assert.IsType 不只驗證型別，
        // 還會回傳轉型後的物件。
        var redirectResult =
            Assert.IsType<RedirectToPageResult>(
                result
            );


        // 確認 Redirect 的目標頁面。
        Assert.Equal(
            "/Admin/Products/Index",
            redirectResult.PageName
        );


        // ========================================
        // Assert：TempData
        // ========================================

        // Peek() 可以讀取 TempData，
        // 而不將該項目標記為已讀取後移除。
        var statusMessage = Assert.IsType<string>(
            pageModel.TempData.Peek("StatusMessage")
        );


        // 確認成功訊息包含正確的商品名稱。
        Assert.Equal(
            "商品「Mechanical Keyboard」已新增。",
            statusMessage
        );


        // ========================================
        // Assert：Database Persistence
        // ========================================

        // 建立另一個 DbContext，
        // 避免只驗證原本 Change Tracker 中的 Entity。
        using var verificationDb =
            database.CreateContext();


        var products = await verificationDb.Products
            .AsNoTracking()
            .ToListAsync();


        // 確認實際新增一筆商品。
        var product = Assert.Single(products);


        // Primary Key 由資料庫產生。
        Assert.True(product.Id > 0);


        // 驗證主要商品欄位。
        Assert.Equal(
            "Mechanical Keyboard",
            product.Title
        );

        Assert.Equal(1500m, product.Price);
        Assert.Equal(10, product.Stock);
        Assert.Equal("electronics", product.Category);
        Assert.True(product.IsFeatured);
    }


    // ==========================================
    // Test 2：Invalid ModelState
    // ==========================================

    /// <summary>
    /// 驗證非法表單提交後：
    ///
    /// 1. 回傳 PageResult
    /// 2. 保留原本的表單資料
    /// 3. 保留 ModelState Error
    /// 4. 不產生成功訊息
    /// 5. 不新增任何商品
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ShouldReturnPageWithoutSaving_WhenModelStateIsInvalid()
    {
        // ========================================
        // Arrange
        // ========================================

        using var database =
            new SqliteTestDatabase();

        using var db = database.CreateContext();


        // 建立 PageModel。
        var pageModel = CreatePageModel(db);


        // 建立表單資料。
        var input = CreateValidInput();


        // 刻意將必填商品名稱設為空字串。
        input.Title = string.Empty;

        pageModel.Input = input;


        // 注意：
        //
        // 直接呼叫 OnPostAsync() 不會經過
        // ASP.NET Core Model Binding Pipeline，
        // 因此 DataAnnotations 不會自動執行。
        //
        // 必須自行加入 ModelState Error，
        // 模擬正式環境驗證失敗的結果。
        pageModel.ModelState.AddModelError(
            "Input.Title",
            "請輸入商品名稱。"
        );


        // 確認測試前提成立。
        Assert.False(pageModel.ModelState.IsValid);


        // ========================================
        // Act
        // ========================================

        var result = await pageModel.OnPostAsync(
            CancellationToken.None
        );


        // ========================================
        // Assert：Handler Result
        // ========================================

        // 驗證 Handler 沒有 Redirect，
        // 而是回傳 PageResult。
        Assert.IsType<PageResult>(result);


        // ========================================
        // Assert：Form Input
        // ========================================

        // Assert.Same 驗證是同一個物件參考。
        //
        // 確認 Handler 沒有重新建立 Input，
        // 導致原本使用者輸入的資料遺失。
        Assert.Same(input, pageModel.Input);

        Assert.Empty(pageModel.Input.Title);

        Assert.Equal(
            "Test keyboard",
            pageModel.Input.Description
        );

        Assert.Equal(1500m, pageModel.Input.Price);


        // ========================================
        // Assert：ModelState Error
        // ========================================

        Assert.False(pageModel.ModelState.IsValid);


        // 確認只有一個 ModelState Error。
        Assert.Equal(
            1,
            pageModel.ModelState.ErrorCount
        );


        // 取得 Input.Title 對應的驗證狀態。
        var titleState =
            pageModel.ModelState["Input.Title"];

        Assert.NotNull(titleState);


        // 確認原本的錯誤訊息仍然存在。
        var error =
            Assert.Single(titleState!.Errors);

        Assert.Equal(
            "請輸入商品名稱。",
            error.ErrorMessage
        );


        // ========================================
        // Assert：TempData
        // ========================================

        // 驗證失敗時，
        // 不應產生商品新增成功訊息。
        Assert.False(
            pageModel.TempData.ContainsKey(
                "StatusMessage"
            )
        );


        // ========================================
        // Assert：Database State
        // ========================================

        // 使用新的 DbContext，
        // 確認資料庫沒有新增商品。
        using var verificationDb =
            database.CreateContext();

        Assert.Equal(
            0,
            await verificationDb.Products.CountAsync()
        );
    }
}
