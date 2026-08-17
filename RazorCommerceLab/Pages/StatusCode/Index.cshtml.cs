using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorCommerceLab.Pages.StatusCode;

/// <summary>
/// HTTP Status Code 錯誤頁。
///
/// 主要用來處理：
///
/// 404 Not Found
///
/// 但設計成可以接收其他 4xx / 5xx Status Code。
///
/// Program.cs：
///
/// app.UseStatusCodePagesWithReExecute(
///     "/StatusCode/{0}");
///
/// 例如原本 Response 是 404：
///
/// /不存在的網址
///       ↓
/// StatusCodePages
///       ↓
/// /StatusCode/404
///       ↓
/// OnGet(404)
/// </summary>
public class IndexModel : PageModel
{
    // ==========================================
    // HTTP Status Code
    // ==========================================

    /// <summary>
    /// 目前要顯示的 HTTP Status Code。
    ///
    /// 例如：
    ///
    /// 404
    /// 403
    /// 400
    /// </summary>
    public int Code { get; private set; }


    // ==========================================
    // Original Path
    // ==========================================

    /// <summary>
    /// 原始發生錯誤的 URL Path。
    ///
    /// 例如使用者輸入：
    ///
    /// /products/abcdef
    ///
    /// Middleware 內部重新執行：
    ///
    /// /StatusCode/404
    ///
    /// 但我們仍然可以取得：
    ///
    /// /products/abcdef
    /// </summary>
    public string? OriginalPath
    { get; private set; }


    // ==========================================
    // Text for View
    // ==========================================

    public string ErrorTitle =>
        Code switch
        {
            404 => "找不到頁面",
            403 => "沒有存取權限",
            400 => "請求內容有誤",
            _ => "無法完成這個請求"
        };


    public string ErrorDescription =>
        Code switch
        {
            404 =>
                "你要找的頁面可能不存在、已被移除，或網址輸入錯誤。",

            403 =>
                "你目前沒有權限存取這個資源。",

            400 =>
                "Server 無法處理目前的 Request。",

            _ =>
                "處理 Request 時發生 HTTP 錯誤。"
        };


    // ==========================================
    // GET /StatusCode/{code}
    // ==========================================

    public void OnGet(
        int code)
    {
        Code =
            code;


        /*
            很重要：

            雖然現在正在 Render：

            /StatusCode/404

            但 HTTP Response 仍然應該是：

            404 Not Found

            而不是：

            200 OK
        */
        Response.StatusCode =
            code;


        // ======================================
        // 取得原始 URL
        // ======================================

        /*
            StatusCodePagesWithReExecute
            不是 Redirect。

            它是在 Server 內部重新執行 Pipeline。

            IStatusCodeReExecuteFeature
            可以讓我們取得：

            原始 Path
            原始 QueryString
        */
        var feature =
            HttpContext.Features
                .Get<IStatusCodeReExecuteFeature>();


        if (feature is not null)
        {
            OriginalPath =
                feature.OriginalPath;
        }
    }
}
