using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RazorCommerceLab.Services;

/// <summary>
/// 專門負責呼叫 DummyJSON Products API。
///
/// 這個 class 不負責：
///
/// - EF Core
/// - SQLite
/// - Product Entity
/// - SaveChanges
///
/// 它的責任只有：
///
/// DummyJSON
///     ↓ HTTP
/// 取得 JSON
///     ↓
/// 反序列化成 C# DTO
///
/// 這樣外部 API 存取邏輯不會全部塞進 SeedEndpoints。
/// </summary>
public sealed class DummyJsonProductClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// HttpClient 不需要自己：
    ///
    /// new HttpClient()
    ///
    /// 我們會在 Program.cs 使用 AddHttpClient()
    /// 把它註冊進 ASP.NET Core DI。
    ///
    /// DI 建立 DummyJsonProductClient 時，
    /// 會自動把已設定好的 HttpClient 傳進來。
    /// </summary>
    public DummyJsonProductClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }


    /// <summary>
    /// 從 DummyJSON 取得指定數量的商品。
    ///
    /// 例如 limit = 100：
    ///
    /// GET
    /// https://dummyjson.com/products?limit=100&skip=0
    /// </summary>
    public async Task<DummyJsonProductsResponse?> GetProductsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        /*
            因為 Program.cs 之後會設定：

            BaseAddress =
                https://dummyjson.com/

            所以這裡只需要寫相對路徑：

            products?limit=100&skip=0
        */
        var requestUri =
            $"products?limit={limit}&skip=0";

        /*
            GetFromJsonAsync<T>()
            同時幫我們做兩件事：

            1. 發送 HTTP GET request
            2. 把 JSON response deserialize 成 T

            所以：

            JSON
              ↓
            DummyJsonProductsResponse
        */
        return await _httpClient
            .GetFromJsonAsync<DummyJsonProductsResponse>(
                requestUri,
                cancellationToken);
    }
}


/// <summary>
/// DummyJSON /products API 最外層 Response。
/// </summary>
public sealed class DummyJsonProductsResponse
{
    /*
        JSON：

        {
            "products": [...]
        }

        ↓

        Products
    */
    [JsonPropertyName("products")]
    public List<DummyJsonProductDto> Products { get; init; } = [];


    [JsonPropertyName("total")]
    public int Total { get; init; }


    [JsonPropertyName("skip")]
    public int Skip { get; init; }


    [JsonPropertyName("limit")]
    public int Limit { get; init; }
}


/// <summary>
/// DummyJSON 單一商品 DTO。
///
/// 注意：
///
/// 這不是我們自己的 Product Entity。
///
/// DummyJsonProductDto
/// → 描述「外部 API 長什麼樣」
///
/// Product
/// → 描述「我們自己的 DB 長什麼樣」
/// </summary>
public sealed class DummyJsonProductDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }


    [JsonPropertyName("title")]
    public string? Title { get; init; }


    [JsonPropertyName("description")]
    public string? Description { get; init; }


    [JsonPropertyName("price")]
    public decimal Price { get; init; }


    [JsonPropertyName("discountPercentage")]
    public decimal DiscountPercentage { get; init; }


    [JsonPropertyName("rating")]
    public decimal Rating { get; init; }


    [JsonPropertyName("stock")]
    public int Stock { get; init; }


    /*
        某些外部商品資料可能沒有 brand，
        所以這裡使用 nullable。
    */
    [JsonPropertyName("brand")]
    public string? Brand { get; init; }


    [JsonPropertyName("category")]
    public string? Category { get; init; }


    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }


    /*
        DummyJSON：

        "images": [
            "https://...",
            "https://..."
        ]
    */
    [JsonPropertyName("images")]
    public List<string>? Images { get; init; }
}
