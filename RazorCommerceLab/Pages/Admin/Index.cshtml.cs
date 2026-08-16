using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorCommerceLab.Pages.Admin;

/// <summary>
/// 後台首頁。
///
/// Step 9 目前只用來驗證：
///
/// - Admin Route
/// - _ViewStart
/// - _AdminLayout
/// - Admin CSS
///
/// 商品管理功能後續才建立。
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
        /*
            目前沒有資料需要準備。

            GET /Admin
            只要直接 Render Page。
        */
    }
}
