using Microsoft.AspNetCore.Mvc.RazorPages;

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
    public void OnGet()
    {
    }
}
