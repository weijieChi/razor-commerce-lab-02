
using RazorCommerceLab.Pages.Admin.Products;
using Xunit;

// 測試專案使用自己的 namespace。
// Unit 表示這個 class 放在單元測試分類。
namespace RazorCommerceLab.Tests.Unit;

/// <summary>
/// 測試 ProductFormHelper 的行為。
///
/// ProductFormHelper 負責：
/// 將後台表單資料轉換成 Product Entity。
///
/// 這組測試不需要：
/// - 啟動 ASP.NET Core
/// - 連線 SQLite
/// - 發送 HTTP Request
/// </summary>
public class ProductFormHelperTests
{
    /// <summary>
    /// 驗證建立商品時，
    /// 商品名稱前後多餘的空白會被移除。
    /// </summary>

    // [Fact] 告訴 xUnit：
    // 下面這個 method 是一個測試案例。
    //
    // 執行 dotnet test 時，
    // xUnit 會找到並執行這個方法。
    [Fact]
    public void CreateProduct_ShouldTrimTitle()
    {
        // ========================================
        // Arrange：準備測試資料
        // ========================================

        // 建立一份後台表單輸入資料。
        //
        // Title 刻意加入前後空白，
        // 用來驗證 CreateProduct 的處理結果。
        var input = new ProductFormInput
        {
            Title = "  Mechanical Keyboard  ",

            // 其他欄位提供正常資料。
            // 這次只關注 Title 的處理行為。
            Description = "Mechanical keyboard",
            Category = "keyboard",
            Price = 1500m,
            Stock = 10
        };


        // ========================================
        // Act：執行被測試的功能
        // ========================================

        // 呼叫正式專案中的方法。
        //
        // 這裡不是自己重新實作 Trim 邏輯，
        // 而是真正執行 ProductFormHelper。
        //
        // CreateProduct 會回傳 Product Entity。
        var product =
            ProductFormHelper.CreateProduct(input);


        // ========================================
        // Assert：驗證執行結果
        // ========================================

        // Assert.Equal(expected, actual)
        //
        // 第一個參數：預期結果。
        // 第二個參數：實際結果。
        //
        // 如果 product.Title 有成功移除前後空白，
        // 就會等於 "Mechanical Keyboard"。
        //
        // 如果不同，xUnit 就會判定測試失敗。
        Assert.Equal(
            "Mechanical Keyboard",
            product.Title
        );
    }
}
