using System.ComponentModel.DataAnnotations;

namespace RazorCommerceLab.Api.Dtos;


// ==========================================
// Request DTO
// ==========================================

/// <summary>
/// 後台新增 / 修改商品時使用的 Request DTO。
///
/// POST /api/admin/products
/// PUT  /api/admin/products/{id}
///
/// 目前 Create 與 Update 的可編輯欄位相同，
/// 所以先共用同一個 DTO。
///
/// 未來如果：
///
/// Create 有欄位 A
/// Update 沒有欄位 A
///
/// 再拆成：
///
/// CreateProductRequest
/// UpdateProductRequest
///
/// 即可。
/// </summary>
public sealed class AdminProductWriteRequest
{
    /// <summary>
    /// 商品名稱。
    ///
    /// 必填，最多 120 字元。
    /// </summary>
    [Required(
        ErrorMessage = "title 為必填欄位。")]
    [StringLength(
        120,
        ErrorMessage = "title 長度不可超過 120 個字元。")]
    public string Title { get; init; } = string.Empty;


    /// <summary>
    /// 商品描述。
    /// </summary>
    [Required(
        ErrorMessage = "description 為必填欄位。")]
    [StringLength(
        2000,
        ErrorMessage = "description 長度不可超過 2000 個字元。")]
    public string Description { get; init; } = string.Empty;


    /// <summary>
    /// 商品價格。
    ///
    /// 不允許負數。
    /// </summary>
    [Range(
        typeof(decimal),
        "0",
        "99999999",
        ErrorMessage = "price 必須介於 0 到 99999999。")]
    public decimal Price { get; init; }


    /// <summary>
    /// 折扣百分比。
    ///
    /// 例如：
    ///
    /// 15.5
    /// = 15.5%
    /// </summary>
    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "discountPercentage 必須介於 0 到 100。")]
    public decimal DiscountPercentage { get; init; }


    /// <summary>
    /// 商品評分。
    ///
    /// DummyJSON 的 rating 大致也是 0 ~ 5。
    /// </summary>
    [Range(
        typeof(decimal),
        "0",
        "5",
        ErrorMessage = "rating 必須介於 0 到 5。")]
    public decimal Rating { get; init; }


    /// <summary>
    /// 商品庫存。
    ///
    /// 可以是 0：
    /// 代表目前沒有庫存。
    /// </summary>
    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "stock 不可小於 0。")]
    public int Stock { get; init; }


    /// <summary>
    /// 品牌可不填。
    /// </summary>
    [StringLength(
        120,
        ErrorMessage = "brand 長度不可超過 120 個字元。")]
    public string? Brand { get; init; }


    /// <summary>
    /// 商品分類。
    /// </summary>
    [Required(
        ErrorMessage = "category 為必填欄位。")]
    [StringLength(
        120,
        ErrorMessage = "category 長度不可超過 120 個字元。")]
    public string Category { get; init; } = string.Empty;


    /// <summary>
    /// 商品縮圖 URL。
    ///
    /// 可以不填。
    ///
    /// 沒圖片時前台仍沿用：
    ///
    /// ThumbnailUrl
    ///     ↓ 空
    /// Picsum
    ///     ↓ 失敗
    /// local placeholder.svg
    /// </summary>
    [StringLength(
        1000,
        ErrorMessage = "thumbnailUrl 長度不可超過 1000 個字元。")]
    public string? ThumbnailUrl { get; init; }


    /// <summary>
    /// 是否在首頁顯示為精選商品。
    /// </summary>
    public bool IsFeatured { get; init; }
}


// ==========================================
// Response DTO
// ==========================================

/// <summary>
/// 後台商品 API 的 Response DTO。
///
/// 它比公開 ProductDto 多了一些後台可能需要的 metadata，
/// 例如 ExternalId、CreatedAt。
/// </summary>
public sealed record AdminProductDto(
    int Id,
    int? ExternalId,
    string Title,
    string Description,
    decimal Price,
    decimal DiscountPercentage,
    decimal Rating,
    int Stock,
    string Brand,
    string Category,
    string ThumbnailUrl,
    bool IsFeatured,
    DateTime CreatedAt
);
