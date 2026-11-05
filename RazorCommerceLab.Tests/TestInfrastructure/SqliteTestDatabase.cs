
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RazorCommerceLab.Infrastructure.Persistence;

namespace RazorCommerceLab.Tests.TestInfrastructure;

/// <summary>
/// 建立並管理 SQLite In-Memory Test Database。
///
/// 每次 new SqliteTestDatabase()：
/// 1. 建立 SQLite Connection
/// 2. 開啟 Connection
/// 3. 設定 EF Core DbContextOptions
/// 4. 建立資料庫 Schema
///
/// 測試結束時，透過 Dispose() 關閉連線。
///
/// 不使用正式專案的 Connection String，
/// 因此不會讀寫 Data/razor-commerce.db。
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    // 保存低階 SQLite Connection。
    //
    // 這裡不能在 Constructor 結束時關閉，
    // 否則記憶體資料庫就會消失。
    private readonly SqliteConnection _connection;

    // 保存 EF Core 設定。
    //
    // 之後可以使用相同 Options
    // 建立多個 AppDbContext。
    private readonly DbContextOptions<AppDbContext> _options;


    public SqliteTestDatabase()
    {
        // 建立記憶體資料庫連線。
        //
        // 這不是檔案路徑，
        // 不會建立實體 SQLite .db 檔案。
        _connection = new SqliteConnection(
            "Data Source=:memory:"
        );

        // 主動開啟連線。
        //
        // 記憶體資料庫會在連線開啟期間存在。
        _connection.Open();

        // 建立 EF Core Options。
        //
        // UseSqlite(_connection)：
        // 指定使用目前已經開啟的 SQLite Connection。
        //
        // 注意：
        // 這裡沒有使用 Program.cs 的
        // ConnectionStrings:DefaultConnection。
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        // 使用測試設定建立 DbContext。
        //
        // using 可確保初始化完成後，
        // 這個 DbContext 正確釋放。
        using var context = CreateContext();

        // 根據 AppDbContext 的 EF Core Model，
        // 建立 Products、CartItems 等資料表。
        //
        // 這裡不執行 Migration，
        // 只是建立目前 Model 對應的 Schema。
        context.Database.EnsureCreated();
    }


    /// <summary>
    /// 建立新的 AppDbContext。
    ///
    /// 每次呼叫都回傳新的 DbContext 物件，
    /// 但使用相同的記憶體資料庫連線。
    ///
    /// 因此可以用不同 DbContext
    /// 驗證資料是否真的保存到資料庫。
    /// </summary>
    public AppDbContext CreateContext()
    {
        return new AppDbContext(_options);
    }


    /// <summary>
    /// 當測試結束時，釋放 SQLite Connection。
    ///
    /// 連線關閉後，
    /// 這個 In-Memory Database 就不再存在。
    /// </summary>
    public void Dispose()
    {
        _connection.Dispose();
    }
}
