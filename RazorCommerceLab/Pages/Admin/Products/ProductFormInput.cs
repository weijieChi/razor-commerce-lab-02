using System.ComponentModel.DataAnnotations;

namespace RazorCommerceLab.Pages.Admin.Products;

/// <summary>
/// 後台商品新增 / 編輯共用的表單模型。
///
/// 注意：
///
/// 這不是 Product Entity。
///
/// Product
/// → 資料庫 Entity
///
/// ProductFormInput
/// → Razor Page 接收表單資料的 Input Model
/// </summary>
public class ProductFormInput
{
    // ==========================================
    // Title
    // ==========================================

    [Display(Name = "商品名稱")]
    [Required(
        ErrorMessage = "請輸入商品名稱。")]
    [StringLength(
        120,
        ErrorMessage = "商品名稱不可超過 120 個字元。")]
    public string Title { get; set; } = string.Empty;


    // ==========================================
    // Description
    // ==========================================

    [Display(Name = "商品描述")]
    [Required(
        ErrorMessage = "請輸入商品描述。")]
    [StringLength(
        2000,
        ErrorMessage = "商品描述不可超過 2000 個字元。")]
    public string Description { get; set; } = string.Empty;


    // ==========================================
    // Price
    // ==========================================

    [Display(Name = "價格")]
    [Range(
        typeof(decimal),
        "0",
        "99999999",
        ErrorMessage = "價格必須介於 0 到 99999999。")]
    public decimal Price { get; set; }


    // ==========================================
    // Discount
    // ==========================================

    [Display(Name = "折扣百分比")]
    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "折扣百分比必須介於 0 到 100。")]
    public decimal DiscountPercentage { get; set; }


    // ==========================================
    // Rating
    // ==========================================

    [Display(Name = "評分")]
    [Range(
        typeof(decimal),
        "0",
        "5",
        ErrorMessage = "評分必須介於 0 到 5。")]
    public decimal Rating { get; set; }


    // ==========================================
    // Stock
    // ==========================================

    [Display(Name = "庫存")]
    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "庫存不可小於 0。")]
    public int Stock { get; set; }


    // ==========================================
    // Brand
    // ==========================================

    [Display(Name = "品牌")]
    [StringLength(
        120,
        ErrorMessage = "品牌不可超過 120 個字元。")]
    public string? Brand { get; set; }


    // ==========================================
    // Category
    // ==========================================

    [Display(Name = "分類")]
    [Required(
        ErrorMessage = "請輸入商品分類。")]
    [StringLength(
        120,
        ErrorMessage = "分類不可超過 120 個字元。")]
    public string Category { get; set; } = string.Empty;


    // ==========================================
    // Thumbnail
    // ==========================================

    [Display(Name = "縮圖網址")]
    [StringLength(
        1000,
        ErrorMessage = "圖片網址不可超過 1000 個字元。")]
    [Url(
        ErrorMessage = "請輸入有效的圖片網址。")]
    public string? ThumbnailUrl { get; set; }


    // ==========================================
    // Featured
    // ==========================================

    [Display(Name = "首頁精選商品")]
    public bool IsFeatured { get; set; }
}
