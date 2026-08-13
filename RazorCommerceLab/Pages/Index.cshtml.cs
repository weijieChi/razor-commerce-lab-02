using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages;

/*
    IndexModel 是 Pages/Index.cshtml 對應的 PageModel。

    .cshtml：
    負責 HTML / Razor markup。

    .cshtml.cs：
    負責處理 request、準備資料、表單邏輯。

    可以先把它想成：

    GET /
      ↓
    IndexModel.OnGet()
      ↓
    Index.cshtml
      ↓
    _Layout.cshtml
      ↓
    HTML Response
*/
public class IndexModel : PageModel
{
    /*
      AppDbContext 由 ASP.NET Core DI 注入。

      Program.cs 已經有：

      builder.Services.AddDbContext<AppDbContext>(...)

      所以這裡不用自己 new AppDbContext。
  */
    private readonly AppDbContext _db;


    /*
      首頁要顯示的精選商品。

      Index.cshtml 裡可以使用：

      Model.FeaturedProducts

      一開始給空集合，
      避免還沒查詢資料時出現 null。
  */
    public IReadOnlyList<Product> FeaturedProducts { get; private set; } = [];


    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    /*
        OnGet() 是 Razor Pages 的 GET Handler。

        當 Browser 發送：

        GET /

        Razor Pages 找到 Pages/Index.cshtml，
        然後執行這個 OnGet()。

        現在首頁還沒有資料要查，
        所以 method 暫時是空的。

        Step 5 加入商品之後，
        這裡會改成 OnGetAsync()，
        並從 EF Core 查詢商品資料。
    */


    //

    /*
     GET /

     Razor Pages 會執行 OnGetAsync()。

     因為這次需要非同步查詢 SQLite，
     所以使用：

     Task OnGetAsync()
 */
    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        /*
            Step 4 Seed 時，
            我們有把部分商品：

            IsFeatured = true

            首頁只撈這些商品。
        */
        FeaturedProducts =
            await _db.Products
                .AsNoTracking()

                .Where(product =>
                    product.IsFeatured)

                .OrderBy(product =>
                    product.Id)

                .Take(8)

                .ToListAsync(
                    cancellationToken);
    }
}
