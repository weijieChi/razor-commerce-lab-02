using System.ComponentModel.DataAnnotations;

namespace RazorCommerceLab.Api.Dtos;


// ==========================================
// Request DTO
// ==========================================

/// <summary>
/// POST /api/cart/items
///
/// 加入商品到購物車時的 Request Body。
///
/// JSON：
///
/// {
///   "productId": 1,
///   "quantity": 2
/// }
/// </summary>
public sealed class AddCartItemRequest
{
    /*
        ProductId 必須 >= 1。

        如果：
        productId = 0

        Minimal API Validation
        會在進入 Endpoint handler 前回 400。
    */
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "productId 必須大於或等於 1。")]
    public int ProductId { get; init; }


    /*
        這個學習專案限制單次數量 1 ~ 99。

        這只是 Request 格式限制。

        「是否超過商品 Stock」
        則屬於商業規則，
        必須到 Endpoint 查 Product 後才能判斷。
    */
    [Range(
        1,
        99,
        ErrorMessage = "quantity 必須介於 1 到 99。")]
    public int Quantity { get; init; }
}


/// <summary>
/// PUT /api/cart/items/{productId}
///
/// 更新購物車商品數量。
/// </summary>
public sealed class UpdateCartItemRequest
{
    [Range(
        1,
        99,
        ErrorMessage = "quantity 必須介於 1 到 99。")]
    public int Quantity { get; init; }
}


// ==========================================
// Response DTO
// ==========================================

/// <summary>
/// 購物車中的一筆商品。
///
/// 不直接回傳 CartItem Entity，
/// 避免把資料庫 Entity 與 API Response 綁死。
/// </summary>
public sealed record CartItemDto(
    int ProductId,
    string Title,
    decimal Price,
    int Quantity,
    decimal LineTotal,
    string ThumbnailUrl
);


/// <summary>
/// 整個購物車 Response。
/// </summary>
public sealed record CartResponse(
    IReadOnlyList<CartItemDto> Items,

    // 所有商品數量總和。
    int TotalQuantity,

    // 所有商品金額總和。
    decimal TotalAmount
);
