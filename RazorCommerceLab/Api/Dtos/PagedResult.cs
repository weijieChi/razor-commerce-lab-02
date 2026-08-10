namespace RazorCommerceLab.Api.Dtos;

/// <summary>
/// 通用分頁結果。
///
/// T 代表「每一筆資料的型別」。
///
/// 例如：
///
/// PagedResult<ProductDto>
///
/// 就代表：
///
/// Items 裡面放 ProductDto。
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,

    // 目前頁碼。
    int Page,

    // 每頁幾筆。
    int PageSize,

    // 符合條件的資料總筆數。
    int TotalItems,

    // 總頁數。
    int TotalPages
);
