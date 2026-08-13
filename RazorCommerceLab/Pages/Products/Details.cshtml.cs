using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Domain;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Products;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;


    /*
        OnGet 成功後一定會有 Product。

        null! 是告訴 nullable analyzer：

        「這個 property 會在 handler
          成功執行後被設定。」
    */
    public Product Product { get; private set; } = null!;


    /*
        同分類推薦商品。
    */
    public IReadOnlyList<Product> RelatedProducts { get; private set; } = [];


    public DetailsModel(AppDbContext db)
    {
        _db = db;
    }


    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        // ======================================
        // 1. 找商品
        // ======================================

        var product =
            await _db.Products
                .AsNoTracking()

                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        // ======================================
        // 2. 不存在 → 404
        // ======================================

        if (product is null)
        {
            /*
                Razor Pages 的 NotFound()
                會產生 HTTP 404。

                Step 13 再把 404
                統一接到自訂 StatusCode Page。
            */
            return NotFound();
        }


        Product = product;


        // ======================================
        // 3. 同分類推薦
        // ======================================

        RelatedProducts =
            await _db.Products
                .AsNoTracking()

                /*
                    同分類。
                */
                .Where(other =>
                    other.Category ==
                        product.Category)

                /*
                    不推薦自己。
                */
                .Where(other =>
                    other.Id != product.Id)

                .OrderBy(other =>
                    other.Id)

                .Take(4)

                .ToListAsync(cancellationToken);


        /*
            回傳目前這個 Razor Page。
        */
        return Page();
    }
}
