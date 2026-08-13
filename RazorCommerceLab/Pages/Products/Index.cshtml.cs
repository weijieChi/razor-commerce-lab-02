using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Products;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    /*
        商品列表每頁固定 12 筆。

        前台目前不讓使用者修改 page size，
        所以不用像 API 一樣接 pageSize query string。
    */
    private const int PageSize = 12;


    // ==========================================
    // Query String
    // ==========================================

    /*
        SupportsGet = true 很重要。

        Razor Pages 的 [BindProperty]
        預設主要處理 POST。

        加上 SupportsGet = true 之後，
        GET Query String 也可以綁定。

        例如：

        /Products?Keyword=phone

        ↓

        Keyword = "phone"
    */
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }


    /*
        /Products?Category=beauty
    */
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }


    /*
        使用 nullable：

        沒有 PageNumber
        → null
        → 稍後使用第一頁

        ?PageNumber=3
        → 3
    */
    [BindProperty(SupportsGet = true)]
    public int? PageNumber { get; set; }


    // ==========================================
    // View data
    // ==========================================

    public IReadOnlyList<Product> Products { get; private set; } = [];

    public IReadOnlyList<string> Categories { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int TotalItems { get; private set; }

    public int TotalPages { get; private set; }


    /*
        Razor View 可以直接：

        Model.HasPreviousPage
    */
    public bool HasPreviousPage =>
        CurrentPage > 1;


    public bool HasNextPage =>
        TotalPages > 0 &&
        CurrentPage < TotalPages;


    public IndexModel(AppDbContext db)
    {
        _db = db;
    }


    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. 決定目前頁碼
        // ======================================

        /*
            沒傳：
            PageNumber = null
            → 1

            傳負數或 0：
            → 同樣修正成 1
        */
        CurrentPage =
            PageNumber is > 0
                ? PageNumber.Value
                : 1;


        // ======================================
        // 2. 讀取分類選單
        // ======================================

        /*
            Categories 下拉選單應該包含
            資料庫目前真的存在的分類。

            SELECT DISTINCT Category
            FROM Products
            ORDER BY Category
        */
        Categories =
            await _db.Products
                .AsNoTracking()

                .Where(product =>
                    product.Category != "")

                .Select(product =>
                    product.Category)

                .Distinct()

                .OrderBy(category =>
                    category)

                .ToListAsync(cancellationToken);


        // ======================================
        // 3. 建立商品 IQueryable
        // ======================================

        var query =
            _db.Products
                .AsNoTracking()
                .AsQueryable();


        // ======================================
        // 4. Keyword
        // ======================================

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var keyword = Keyword.Trim();

            query =
                query.Where(product =>
                    product.Title.Contains(keyword) ||
                    product.Description.Contains(keyword) ||
                    product.Brand.Contains(keyword));
        }


        // ======================================
        // 5. Category
        // ======================================

        if (!string.IsNullOrWhiteSpace(Category))
        {
            var category = Category.Trim();

            query =
                query.Where(product =>
                    product.Category == category);
        }


        // ======================================
        // 6. Count
        // ======================================

        TotalItems =
            await query.CountAsync(cancellationToken);


        TotalPages =
            TotalItems == 0
                ? 0
                : (int)Math.Ceiling(
                    TotalItems / (double)PageSize);


        // ======================================
        // 7. 超過最後一頁時修正
        // ======================================

        /*
            例如：

            只有 3 頁

            使用者手動輸入：

            ?PageNumber=999

            我們把它修正到最後一頁。
        */
        if (TotalPages > 0 &&
            CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }


        // ======================================
        // 8. Pagination
        // ======================================

        Products =
            await query
                .OrderBy(product => product.Id)

                .Skip(
                    (CurrentPage - 1) * PageSize)

                .Take(PageSize)

                .ToListAsync(cancellationToken);
    }
}
