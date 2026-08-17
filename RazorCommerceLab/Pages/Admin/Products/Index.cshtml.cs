using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Admin.Products;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;


    /*
        後台列表固定每頁 12 筆。

        目前不讓使用者自己改 PageSize，
        這樣 PageModel 會比較單純。
    */
    private const int PageSize = 12;


    // ==========================================
    // Query String
    // ==========================================

    /*
        GET /Admin/Products?Keyword=phone

        因為 SupportsGet = true，
        Query String 可以自動綁定到 Keyword。

        Razor Pages 對 GET property binding
        必須明確啟用 SupportsGet。
    */
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }


    /*
        GET /Admin/Products?Category=beauty
    */
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }


    /*
        使用 nullable int?：

        沒傳 PageNumber
        → null
        → 稍後套用第 1 頁。

        ?PageNumber=3
        → 3
    */
    [BindProperty(SupportsGet = true)]
    public int? PageNumber { get; set; }


    // ==========================================
    // Data for View
    // ==========================================

    /*
        不直接把 Product Entity
        整個丟給 View。

        列表只準備真正需要的欄位。
    */
    public IReadOnlyList<AdminProductListItem> Products
    { get; private set; } = [];


    /*
        Category 下拉選單。
    */
    public IReadOnlyList<string> Categories
    { get; private set; } = [];


    /*
        分頁資料。
    */
    public int CurrentPage { get; private set; } = 1;

    public int TotalItems { get; private set; }

    public int TotalPages { get; private set; }


    public bool HasPreviousPage =>
        CurrentPage > 1;


    public bool HasNextPage =>
        TotalPages > 0 &&
        CurrentPage < TotalPages;


    public IndexModel(AppDbContext db)
    {
        _db = db;
    }


    // ==========================================
    // GET /Admin/Products
    // ==========================================

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. Page Number
        // ======================================

        /*
            沒有 PageNumber：
            → 第 1 頁。

            PageNumber <= 0：
            → 同樣修正成第 1 頁。
        */
        CurrentPage =
            PageNumber is > 0
                ? PageNumber.Value
                : 1;


        // ======================================
        // 2. Category options
        // ======================================

        /*
            從 Products table
            找目前真正存在的分類。

            概念 SQL：

            SELECT DISTINCT Category
            FROM Products
            WHERE Category <> ''
            ORDER BY Category;
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

                .ToListAsync(
                    cancellationToken);


        // ======================================
        // 3. Base Query
        // ======================================

        /*
            後台列表只是讀資料，
            所以使用 AsNoTracking()。
        */
        var query =
            _db.Products
                .AsNoTracking()
                .AsQueryable();


        // ======================================
        // 4. Keyword Search
        // ======================================

        if (!string.IsNullOrWhiteSpace(
                Keyword))
        {
            var keyword =
                Keyword.Trim();


            /*
                搜尋：

                Title
                Description
                Brand
            */
            query =
                query.Where(product =>
                    product.Title.Contains(keyword) ||
                    product.Description.Contains(keyword) ||
                    product.Brand.Contains(keyword));
        }


        // ======================================
        // 5. Category Filter
        // ======================================

        if (!string.IsNullOrWhiteSpace(
                Category))
        {
            var category =
                Category.Trim();


            query =
                query.Where(product =>
                    product.Category == category);
        }


        // ======================================
        // 6. Count
        // ======================================

        /*
            Count 一定要在 Skip / Take 之前。

            這樣 TotalItems 才代表：

            「所有符合搜尋條件的商品」

            而不是只有目前這一頁。
        */
        TotalItems =
            await query.CountAsync(
                cancellationToken);


        // ======================================
        // 7. Total Pages
        // ======================================

        TotalPages =
            TotalItems == 0
                ? 0
                : (int)Math.Ceiling(
                    TotalItems /
                    (double)PageSize);


        // ======================================
        // 8. Page overflow
        // ======================================

        /*
            例如：

            總共只有 4 頁

            使用者手動輸入：
            ?PageNumber=999

            就把它調整到第 4 頁。
        */
        if (TotalPages > 0 &&
            CurrentPage > TotalPages)
        {
            CurrentPage =
                TotalPages;
        }


        // ======================================
        // 9. Query current page
        // ======================================

        Products =
            await query

                /*
                    後台通常希望最近新增的商品
                    先出現在上方。

                    所以這裡使用 Id DESC。
                */
                .OrderByDescending(product =>
                    product.Id)

                /*
                    Page 1：
                    Skip(0)

                    Page 2：
                    Skip(12)

                    Page 3：
                    Skip(24)
                */
                .Skip(
                    (CurrentPage - 1) *
                    PageSize)

                .Take(PageSize)

                /*
                    Entity
                    ↓
                    AdminProductListItem

                    DB 只需要 SELECT
                    列表真正需要的欄位。
                */
                .Select(product =>
                    new AdminProductListItem(
                        Id:
                            product.Id,

                        ExternalId:
                            product.ExternalId,

                        Title:
                            product.Title,

                        Category:
                            product.Category,

                        Price:
                            product.Price,

                        Stock:
                            product.Stock,

                        ThumbnailUrl:
                            product.ThumbnailUrl,

                        IsFeatured:
                            product.IsFeatured,

                        CreatedAt:
                            product.CreatedAt
                    ))

                .ToListAsync(
                    cancellationToken);
    }


    // ==========================================
    // List ViewModel
    // ==========================================

    /// <summary>
    /// Admin 商品列表真正需要的欄位。
    ///
    /// 它不是 Product Entity，
    /// 也不是 Step 10 的 API DTO。
    ///
    /// 只服務這個 Razor Page。
    /// </summary>
    public sealed record AdminProductListItem(
        int Id,
        int? ExternalId,
        string Title,
        string Category,
        decimal Price,
        int Stock,
        string ThumbnailUrl,
        bool IsFeatured,
        DateTime CreatedAt
    );
}
