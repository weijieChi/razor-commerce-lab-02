using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Admin.Products;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;


    /*
        顯示目前正在編輯的 Product Id。

        不拿來 Model Binding。
    */
    public int ProductId
    { get; private set; }


    [BindProperty]
    public ProductFormInput Input
    { get; set; } = new();


    public EditModel(
        AppDbContext db)
    {
        _db = db;
    }


    // ==========================================
    // GET /Admin/Products/Edit/5
    // ==========================================

    public async Task<IActionResult>
        OnGetAsync(
            int id,
            CancellationToken cancellationToken)
    {
        var product =
            await _db.Products
                .AsNoTracking()

                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        if (product is null)
        {
            return NotFound();
        }


        ProductId =
            product.Id;


        /*
            Product
            ↓
            ProductFormInput

            讓表單顯示目前 DB 的內容。
        */
        Input =
            ProductFormHelper.FromProduct(
                product);


        return Page();
    }


    // ==========================================
    // POST /Admin/Products/Edit/5
    // ==========================================

    public async Task<IActionResult>
        OnPostAsync(
            int id,
            CancellationToken cancellationToken)
    {
        /*
            驗證失敗時仍要設定 ProductId，
            因為 View 還要顯示它。
        */
        ProductId =
            id;


        if (!ModelState.IsValid)
        {
            return Page();
        }


        // ======================================
        // Find tracked Product
        // ======================================

        /*
            這次要修改 Entity，
            所以不使用 AsNoTracking()。
        */
        var product =
            await _db.Products
                .FirstOrDefaultAsync(
                    product =>
                        product.Id == id,
                    cancellationToken);


        if (product is null)
        {
            return NotFound();
        }


        // ======================================
        // Input → Entity
        // ======================================

        ProductFormHelper.ApplyToProduct(
            product,
            Input);


        /*
            product 是目前 DbContext
            查詢出的 tracked entity。

            所以不用：

            _db.Products.Update(product);
        */
        await _db.SaveChangesAsync(
            cancellationToken);


        TempData["StatusMessage"] =
            $"商品「{product.Title}」已更新。";


        return RedirectToPage(
            "/Admin/Products/Index");
    }
}
