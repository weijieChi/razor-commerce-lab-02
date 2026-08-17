using RazorCommerceLab.Domain;

namespace RazorCommerceLab.Pages.Admin.Products;

/// <summary>
/// Product Entity 與 ProductFormInput
/// 之間的轉換集中在這裡。
///
/// 目的：
///
/// Create Page
/// Edit Page
///
/// 不需要各自重複寫一大串 mapping。
/// </summary>
public static class ProductFormHelper
{
    // ==========================================
    // Product → Form Input
    // ==========================================

    /// <summary>
    /// 編輯商品時：
    ///
    /// Product Entity
    ///       ↓
    /// ProductFormInput
    ///
    /// 讓目前資料顯示在 Edit 表單。
    /// </summary>
    public static ProductFormInput FromProduct(
        Product product)
    {
        return new ProductFormInput
        {
            Title =
                product.Title,

            Description =
                product.Description,

            Price =
                product.Price,

            DiscountPercentage =
                product.DiscountPercentage,

            Rating =
                product.Rating,

            Stock =
                product.Stock,

            Brand =
                product.Brand,

            Category =
                product.Category,

            ThumbnailUrl =
                product.ThumbnailUrl,

            IsFeatured =
                product.IsFeatured
        };
    }


    // ==========================================
    // Form Input → New Product
    // ==========================================

    /// <summary>
    /// 新增商品時建立 Product Entity。
    /// </summary>
    public static Product CreateProduct(
        ProductFormInput input)
    {
        var product =
            new Product
            {
                /*
                    後台人工新增商品沒有
                    DummyJSON ExternalId。
                */
                ExternalId = null,


                /*
                    目前後台沒有做多圖片管理。
                */
                ImageUrlsJson = "[]",


                /*
                    CreatedAt 是系統資料，
                    不讓表單自己輸入。
                */
                CreatedAt =
                    DateTime.UtcNow
            };


        /*
            共用下面的 mapping。
        */
        ApplyToProduct(
            product,
            input);


        return product;
    }


    // ==========================================
    // Form Input → Existing Product
    // ==========================================

    /// <summary>
    /// 將表單中允許修改的欄位
    /// 套用到 Product。
    ///
    /// 不會修改：
    ///
    /// Id
    /// ExternalId
    /// ImageUrlsJson
    /// CreatedAt
    /// </summary>
    public static void ApplyToProduct(
        Product product,
        ProductFormInput input)
    {
        product.Title =
            input.Title.Trim();

        product.Description =
            input.Description.Trim();

        product.Price =
            input.Price;

        product.DiscountPercentage =
            input.DiscountPercentage;

        product.Rating =
            input.Rating;

        product.Stock =
            input.Stock;

        product.Brand =
            input.Brand?.Trim()
            ?? string.Empty;

        product.Category =
            input.Category.Trim();

        /*
            沒輸入圖片就存空字串。

            前台已經有自己的 fallback：

            ThumbnailUrl 空
            ↓
            Picsum
            ↓
            本機 placeholder

            所以不要把 placeholder URL
            寫死到每一筆 DB 資料。
        */
        product.ThumbnailUrl =
            input.ThumbnailUrl?.Trim()
            ?? string.Empty;

        product.IsFeatured =
            input.IsFeatured;
    }
}
