using System.ComponentModel.DataAnnotations;

namespace RazorCommerceLab.Api;

/// <summary>
/// GET /api/products 的 Query String DTO。
///
/// 範例：
///
/// /api/products?page=2&pageSize=10
///
/// /api/products?keyword=phone
///
/// /api/products?category=beauty
///
/// /api/products?keyword=wood&category=furniture&page=2&pageSize=10
///
/// [AsParameters] 會負責把 Query String
/// 綁定到這個物件。
///
/// DataAnnotations 則負責驗證綁定完成後的資料是否合法。
/// </summary>
public sealed class ProductQuery
{
    /// <summary>
    /// 商品搜尋關鍵字。
    ///
    /// nullable：
    /// 使用者可以完全不傳 keyword。
    ///
    /// 如果有傳，最多 100 個字元。
    /// </summary>
    [StringLength(
        100,
        ErrorMessage = "keyword 長度不可超過 100 個字元。")]
    public string? Keyword { get; init; }


    /// <summary>
    /// 商品分類。
    ///
    /// 使用者可以不指定分類，
    /// 但有指定時最多 100 個字元。
    /// </summary>
    [StringLength(
        100,
        ErrorMessage = "category 長度不可超過 100 個字元。")]
    public string? Category { get; init; }


    /// <summary>
    /// 頁碼。
    ///
    /// 預設：
    /// page = 1
    ///
    /// [Range] 保證頁碼至少從 1 開始。
    /// </summary>
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "page 必須大於或等於 1。")]
    public int Page { get; init; } = 1;


    /// <summary>
    /// 每頁顯示筆數。
    ///
    /// 預設：
    /// pageSize = 12
    ///
    /// 最大限制 100，
    /// 避免 Client 一次要求過大量資料。
    /// </summary>
    [Range(
        1,
        100,
        ErrorMessage = "pageSize 必須介於 1 到 100。")]
    public int PageSize { get; init; } = 12;
}
