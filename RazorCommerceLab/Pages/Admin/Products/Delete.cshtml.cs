using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Admin.Products;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;


    /*
        刪除確認頁只需要顯示少量資料，
        不需要整個 Product Entity。
    */
    public ProductDeleteView? Product
    { get; private set; }


    public DeleteModel(
        AppDbContext db)
    {
        _db = db;
    }


    // ==========================================
    // GET /Admin/Products/Delete/5
    // ==========================================

    public async Task<IActionResult>
        OnGetAsync(
            int id,
            CancellationToken cancellationToken)
    {
        Product =
            await _db.Products
                .AsNoTracking()

                .Where(product =>
                    product.Id == id)

                .Select(product =>
                    new ProductDeleteView(
                        product.Id,
                        product.Title,
                        product.Category,
                        product.Price,
                        product.Stock))

                .FirstOrDefaultAsync(
                    cancellationToken);


        if (Product is null)
        {
            return NotFound();
        }


        return Page();
    }


    // ==========================================
    // POST /Admin/Products/Delete/5
    // ==========================================

    public async Task<IActionResult>
        OnPostAsync(
            int id,
            CancellationToken cancellationToken)
    {
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


        var productTitle =
            product.Title;


        // ======================================
        // DELETE
        // ======================================

        _db.Products.Remove(
            product);


        await _db.SaveChangesAsync(
            cancellationToken);


        TempData["StatusMessage"] =
            $"商品「{productTitle}」已刪除。";


        return RedirectToPage(
            "/Admin/Products/Index");
    }


    public sealed record ProductDeleteView(
        int Id,
        string Title,
        string Category,
        decimal Price,
        int Stock
    );
}
