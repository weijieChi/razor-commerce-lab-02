using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Pages.Admin.Products;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;


    // ==========================================
    // Form Binding
    // ==========================================

    /*
        POST Form：

        Input.Title
        Input.Price
        Input.Stock
        ...

        ↓ Model Binding

        Input property
    */
    [BindProperty]
    public ProductFormInput Input
    { get; set; } = new();


    public CreateModel(
        AppDbContext db)
    {
        _db = db;
    }


    // ==========================================
    // GET /Admin/Products/Create
    // ==========================================

    public void OnGet()
    {
        /*
            新增表單預設值。

            ThumbnailUrl 留空，
            不把 placeholder URL
            寫入資料庫。
        */
        Input =
            new ProductFormInput
            {
                Price = 0,
                DiscountPercentage = 0,
                Rating = 0,
                Stock = 0,
                ThumbnailUrl = string.Empty,
                IsFeatured = false
            };
    }


    // ==========================================
    // POST /Admin/Products/Create
    // ==========================================

    public async Task<IActionResult>
        OnPostAsync(
            CancellationToken cancellationToken)
    {
        /*
            Model Binding 完成後，
            DataAnnotations 驗證結果
            都存在 ModelState。
        */
        if (!ModelState.IsValid)
        {
            /*
                驗證失敗：

                不 Redirect。

                直接重新 render 同一頁，
                這樣使用者輸入的內容和
                validation errors 都會保留。
            */
            return Page();
        }


        // ======================================
        // Input → Product
        // ======================================

        var product =
            ProductFormHelper.CreateProduct(
                Input);


        // ======================================
        // INSERT
        // ======================================

        _db.Products.Add(
            product);


        await _db.SaveChangesAsync(
            cancellationToken);


        // ======================================
        // Redirect message
        // ======================================

        TempData["StatusMessage"] =
            $"商品「{product.Title}」已新增。";


        /*
            POST
            ↓
            Redirect
            ↓
            GET /Admin/Products

            避免重新整理時再次送 POST。
        */
        return RedirectToPage(
            "/Admin/Products/Index");
    }
}
