
using RazorCommerceLab.Domain;
using RazorCommerceLab.Pages.Admin.Products;
using Xunit;

namespace RazorCommerceLab.Tests.Unit;

/// <summary>
/// ProductFormHelper 的單元測試。
///
/// 被測試的功能：
/// 1. 建立 Product
/// 2. 將 Product 轉成表單資料
/// 3. 修改既有 Product
///
/// 本測試完全不使用資料庫或 HTTP。
/// </summary>
public class ProductFormHelperTests
{
    // ==========================================
    // Test 1：Trim 商品名稱
    // ==========================================

    /// <summary>
    /// 建立商品時，Title 前後的空白應被移除。
    /// 這是 Step 16-2 已完成的測試。
    /// 方法：CreateProduct
    /// 預期：ShouldTrimTitle
    /// 條件：WhenInputHasSurroundingWhitespace
    ///
    /// 驗證當商品名稱前後包含空白時，
    /// CreateProduct 應移除這些空白。
    /// </summary>
    [Fact]
    public void CreateProduct_ShouldTrimTitle_WhenInputHasSurroundingWhitespace()
    {
        // ========================================
        // Arrange
        // ========================================

        // 建立這個 Test 專屬的 Input。
        //
        // 雖然 CreateValidInput 是共用方法，
        // 但每次呼叫都會 new 一個新物件。
        var input = CreateValidInput();

        // 只修改這次要測試的欄位。
        input.Title = "  Mechanical Keyboard  ";


        // ========================================
        // Act
        // ========================================

        // 執行正式專案的 CreateProduct。
        var product = ProductFormHelper.CreateProduct(input);


        // ========================================
        // Assert
        // ========================================

        // 驗證建立後的 Title 已去除前後空白。
        Assert.Equal("Mechanical Keyboard", product.Title);
    }


    // ==========================================
    // Test 2：Trim 其他文字欄位
    // ==========================================

    /// <summary>
    /// 除了 Title，其他文字欄位也應去除前後空白。
    ///
    /// 將具有相同規則的欄位集中在一個測試中，
    /// 不必每個欄位都建立獨立的 Test Method。
    /// </summary>
    [Fact]
    public void CreateProduct_ShouldTrimOtherTextFields()
    {
        // Arrange
        var input = new ProductFormInput
        {
            Title = "Keyboard",
            Description = "  Gaming keyboard  ",
            Category = "  keyboard  ",
            Brand = "  Test Brand  ",
            ThumbnailUrl = "  https://example.com/image.png  "
        };

        // Act
        var product = ProductFormHelper.CreateProduct(input);

        // Assert
        // 這次集中驗證其他文字欄位的 Trim 行為。
        Assert.Equal("Gaming keyboard", product.Description);
        Assert.Equal("keyboard", product.Category);
        Assert.Equal("Test Brand", product.Brand);
        Assert.Equal(
            "https://example.com/image.png",
            product.ThumbnailUrl
        );
    }


    // ==========================================
    // Test 3：Nullable 欄位
    // ==========================================

    /// <summary>
    /// Brand 和 ThumbnailUrl 都允許 null。
    ///
    /// 但是 Product Entity 使用空字串代表沒有資料。
    ///
    /// 所以當表單傳入 null 時，
    /// Helper 應轉換成 string.Empty。
    /// </summary>
    [Fact]
    public void CreateProduct_ShouldUseEmptyStrings_WhenOptionalFieldsAreNull()
    {
        // ========================================
        // Arrange
        // ========================================

        // 再呼叫一次 CreateValidInput。
        //
        // 這個 input 與其他測試裡建立的 input
        // 完全不是同一個物件。
        var input = CreateValidInput();

        // 故意將兩個 Optional Fields 改成 null。
        //
        // 此修改只會影響目前這個 Test 的 input。
        input.Brand = null;
        input.ThumbnailUrl = null;


        // ========================================
        // Act
        // ========================================

        var product = ProductFormHelper.CreateProduct(input);


        // ========================================
        // Assert
        // ========================================

        // 確認 null 會被轉換成空字串。
        Assert.Equal(string.Empty, product.Brand);
        Assert.Equal(string.Empty, product.ThumbnailUrl);
    }

    // ==========================================
    // Test 4：新增商品的系統欄位
    // ==========================================

    /// <summary>
    /// 後台人工建立的商品：
    ///
    /// ExternalId = null
    /// ImageUrlsJson = "[]"
    /// CreatedAt = UTC 建立時間
    ///
    /// 這些欄位不是由表單使用者自行決定。
    /// </summary>
    [Fact]
    public void CreateProduct_ShouldInitializeSystemFields()
    {
        // Arrange
        var input = new ProductFormInput
        {
            Title = "Keyboard",
            Description = "Test product",
            Category = "keyboard"
        };

        // 記錄執行 CreateProduct 前的 UTC 時間。
        //
        // 不直接把時間寫死，
        // 因為每次執行測試的時間都不同。
        var before = DateTime.UtcNow;

        // Act
        var product = ProductFormHelper.CreateProduct(input);

        // 記錄執行結束後的 UTC 時間。
        var after = DateTime.UtcNow;

        // Assert

        // 後台商品不具有 DummyJSON ExternalId。
        Assert.Null(product.ExternalId);

        // 新商品的其他圖片預設是空 JSON Array。
        Assert.Equal("[]", product.ImageUrlsJson);

        // CreatedAt 應落在測試方法呼叫前後的區間內。
        Assert.InRange(product.CreatedAt, before, after);

        // 檢查 DateTime Kind 是否為 UTC。
        Assert.Equal(DateTimeKind.Utc, product.CreatedAt.Kind);
    }


    // ==========================================
    // Test 5：Product → ProductFormInput
    // ==========================================

    /// <summary>
    /// FromProduct() 應將商品的可編輯欄位
    /// 正確複製到 ProductFormInput。
    ///
    /// 用於 Razor Edit Page 預先填入表單。
    /// </summary>
    [Fact]
    public void FromProduct_ShouldCopyEditableFields()
    {
        // Arrange
        // 建立測試用 Product。
        //
        // 這只是 C# 物件，
        // 並沒有寫入 SQLite。
        var product = new Product
        {
            Id = 10,
            Title = "Keyboard",
            Description = "Mechanical keyboard",
            Price = 1500m,
            DiscountPercentage = 10m,
            Rating = 4.5m,
            Stock = 20,
            Brand = "Test Brand",
            Category = "keyboard",
            ThumbnailUrl = "https://example.com/image.png",
            IsFeatured = true
        };

        // Act
        // 將 Product 轉成表單輸入模型。
        var input = ProductFormHelper.FromProduct(product);

        // Assert
        // 驗證所有可編輯欄位都有正確帶入。
        Assert.Equal(product.Title, input.Title);
        Assert.Equal(product.Description, input.Description);
        Assert.Equal(product.Price, input.Price);
        Assert.Equal(
            product.DiscountPercentage,
            input.DiscountPercentage
        );
        Assert.Equal(product.Rating, input.Rating);
        Assert.Equal(product.Stock, input.Stock);
        Assert.Equal(product.Brand, input.Brand);
        Assert.Equal(product.Category, input.Category);
        Assert.Equal(product.ThumbnailUrl, input.ThumbnailUrl);
        Assert.Equal(product.IsFeatured, input.IsFeatured);
    }


    // ==========================================
    // Test 6：更新商品可編輯欄位
    // ==========================================

    /// <summary>
    /// ApplyToProduct() 應將表單資料
    /// 套用到原本的 Product。
    ///
    /// 注意：
    /// 這個方法直接修改傳入的 Product 物件，
    /// 不會回傳新的 Product。
    /// </summary>
    [Fact]
    public void ApplyToProduct_ShouldUpdateEditableFields()
    {
        // Arrange
        // 原本的商品資料。
        var product = new Product
        {
            Title = "Old Keyboard",
            Description = "Old description",
            Price = 1000m,
            DiscountPercentage = 0m,
            Rating = 3m,
            Stock = 5,
            Brand = "Old Brand",
            Category = "old",
            ThumbnailUrl = string.Empty,
            IsFeatured = false
        };

        // 管理員提交的新表單資料。
        var input = new ProductFormInput
        {
            Title = "  New Keyboard  ",
            Description = "  Updated description  ",
            Price = 2000m,
            DiscountPercentage = 20m,
            Rating = 4.8m,
            Stock = 30,
            Brand = "  New Brand  ",
            Category = "  keyboard  ",
            ThumbnailUrl = "  https://example.com/new.png  ",
            IsFeatured = true
        };

        // Act
        // 直接修改既有的 product 物件。
        ProductFormHelper.ApplyToProduct(product, input);

        // Assert
        // 檢查每個允許修改的欄位。
        Assert.Equal("New Keyboard", product.Title);
        Assert.Equal("Updated description", product.Description);
        Assert.Equal(2000m, product.Price);
        Assert.Equal(20m, product.DiscountPercentage);
        Assert.Equal(4.8m, product.Rating);
        Assert.Equal(30, product.Stock);
        Assert.Equal("New Brand", product.Brand);
        Assert.Equal("keyboard", product.Category);
        Assert.Equal(
            "https://example.com/new.png",
            product.ThumbnailUrl
        );
        Assert.True(product.IsFeatured);
    }


    // ==========================================
    // Test 7：保護系統欄位
    // ==========================================

    /// <summary>
    /// 編輯商品時，不能破壞系統管理的欄位：
    ///
    /// Id
    /// ExternalId
    /// ImageUrlsJson
    /// CreatedAt
    ///
    /// 方法：ApplyToProduct
    /// 預期：ShouldPreserveSystemFields
    /// 條件：WhenUpdatingExistingProduct
    ///
    /// 這是這組測試中很重要的資料完整性規則。
    /// 修改既有商品時，不應修改系統管理的欄位。
    /// </summary>
    [Fact]
    public void ApplyToProduct_ShouldPreserveSystemFields_WhenUpdatingExistingProduct()
    {
        // Arrange

        // 固定一個建立時間，方便測試前後是否相同。
        var originalCreatedAt = new DateTime(
            2025, 5, 20, 10, 30, 0, DateTimeKind.Utc
        );

        // 模擬資料庫已經存在的商品。
        var product = new Product
        {
            Id = 50,
            ExternalId = 123,
            Title = "Old Product",
            Description = "Old description",
            Category = "old",
            ImageUrlsJson = "[\"existing.png\"]",
            CreatedAt = originalCreatedAt
        };

        // 表單只能提供可編輯欄位。
        var input = new ProductFormInput
        {
            Title = "Updated Product",
            Description = "Updated description",
            Category = "keyboard"
        };

        // Act
        ProductFormHelper.ApplyToProduct(product, input);

        // Assert

        // 先確認方法確實有套用表單資料。
        Assert.Equal("Updated Product", product.Title);

        // 然後確認系統欄位沒有被修改。
        Assert.Equal(50, product.Id);
        Assert.Equal(123, product.ExternalId);
        Assert.Equal("[\"existing.png\"]", product.ImageUrlsJson);
        Assert.Equal(originalCreatedAt, product.CreatedAt);
    }

    /// <summary>
    /// 建立一份有效的 ProductFormInput 測試資料。
    ///
    /// 這個方法只負責建立新物件，
    /// 不會使用 static field 保存共用物件。
    ///
    /// 每次呼叫都會產生獨立的 ProductFormInput。
    /// </summary>
    private static ProductFormInput CreateValidInput()
    {
        return new ProductFormInput
        {
            Title = "Keyboard",
            Description = "Test product",
            Category = "keyboard",
            Price = 1500m,
            Stock = 10,
            Brand = "Test Brand",
            ThumbnailUrl = "https://example.com/image.png"
        };
    }
}
