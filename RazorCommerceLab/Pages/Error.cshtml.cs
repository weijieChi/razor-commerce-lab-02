using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorCommerceLab.Pages;

/// <summary>
/// ASP.NET Core 未處理 Exception 的錯誤頁。
///
/// Program.cs：
///
/// app.UseExceptionHandler("/Error");
///
/// 當下游 Middleware / Razor Page / Endpoint
/// 發生沒有處理的 Exception 時：
///
/// Exception
///     ↓
/// ExceptionHandler Middleware
///     ↓
/// /Error
///     ↓
/// 500 Error Page
/// </summary>
public class ErrorModel : PageModel
{
    // ==========================================
    // Request ID
    // ==========================================

    /// <summary>
    /// 每次 Request 的識別碼。
    ///
    /// 發生正式環境錯誤時，
    /// 可以把 RequestId 放進 Log，
    /// 之後較容易追查。
    /// </summary>
    public string? RequestId
    { get; private set; }


    public bool ShowRequestId =>
        !string.IsNullOrEmpty(
            RequestId);


    // ==========================================
    // Original Path
    // ==========================================

    /// <summary>
    /// 發生 Exception 的原始 URL。
    /// </summary>
    public string? OriginalPath
    { get; private set; }


    // ==========================================
    // GET /Error
    // ==========================================

    public void OnGet()
    {
        /*
            ExceptionHandler 本身會處理 500，

            這裡再明確設定，
            方便這個教學專案理解：

            Error Page
            ≠ 正常 200 Page
        */
        Response.StatusCode =
            StatusCodes.Status500InternalServerError;


        // ======================================
        // Request ID
        // ======================================

        /*
            Activity.Current?.Id：

            如果目前有 tracing Activity，
            優先使用它。

            否則使用：

            HttpContext.TraceIdentifier
        */
        RequestId =
            Activity.Current?.Id
            ?? HttpContext.TraceIdentifier;


        // ======================================
        // Exception information
        // ======================================

        /*
            ExceptionHandler Middleware
            會把原始 Exception 的資訊
            放在 HttpContext.Features。

            這裡只取得原始 Path。

            不把 exception.Message
            或 StackTrace 顯示給一般使用者。
        */
        var exceptionFeature =
            HttpContext.Features
                .Get<IExceptionHandlerPathFeature>();


        OriginalPath =
            exceptionFeature?.Path;
    }
}
