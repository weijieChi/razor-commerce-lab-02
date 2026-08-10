namespace RazorCommerceLab.Api.Dtos;

/// <summary>
/// 商品 API 對外回傳的 DTO。
///
/// DTO = Data Transfer Object
///
/// 它不是 EF Core Entity，
/// 只是用來定義 API 要回傳哪些資料。
///
/// Product Entity
///      ↓
/// ProductDto
///      ↓
/// JSON Response
///
/// 好處是：
///
/// 1. API 不必把資料庫 Entity 的所有欄位暴露出去。
/// 2. 未來 Entity 改變，不一定會影響 API 格式。
/// 3. 可以針對前端需求重新整理資料格式。
/// </summary>
public sealed record ProductDto(
    int Id,
    string Title,
    string Description,
    decimal Price,
    decimal DiscountPercentage,
    decimal Rating,
    int Stock,
    string Brand,
    string Category,
    string ThumbnailUrl,
    bool IsFeatured
);

/*
這裡使用
因為 DTO 主要用途是：

裝資料
傳資料

通常不需要太多行為。

所以
Entity
→ 資料庫需要什麼

DTO
→ Client 需要什麼
*/
