using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RazorCommerceLab.Domain;

namespace RazorCommerceLab.Infrastructure.Persistence;

/// <summary>
/// AppDbContext 是 EF Core 與資料庫之間的主要入口。
///
/// 可以先把它想成：
///
/// Razor Page / Minimal API
///          ↓
///     AppDbContext
///          ↓
///       EF Core
///          ↓
///       SQLite
///
/// 之後我們不會自己寫：
///
/// new SqliteConnection(...)
///
/// 大部分 CRUD 都會透過 AppDbContext 操作。
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// DbContextOptions 會由 ASP.NET Core DI 傳入。
    ///
    /// 裡面包含像是：
    ///
    /// - 使用 SQLite provider
    /// - Connection String
    /// - EF Core options
    ///
    /// 所以 AppDbContext 不需要自己硬寫資料庫連線資訊。
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }


    /// <summary>
    /// Products 對應 Product Entity。
    ///
    /// 之後可以寫：
    ///
    /// db.Products
    ///
    /// 例如：
    ///
    /// await db.Products.ToListAsync();
    /// </summary>
    public DbSet<Product> Products => Set<Product>();


    /// <summary>
    /// CartItems 對應 CartItem Entity。
    /// </summary>
    public DbSet<CartItem> CartItems => Set<CartItem>();


    /// <summary>
    /// OnModelCreating 是 EF Core 建立 Model 時呼叫的方法。
    ///
    /// 我們在這裡使用 Fluent API，
    /// 告訴 EF Core：
    ///
    /// - 哪個欄位是 PK
    /// - 哪些欄位 required
    /// - 最大長度
    /// - 哪些欄位要建立 index
    /// - Product / CartItem 的關係
    ///
    /// 之後 Migration 就會根據這些設定產生 schema。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureProduct(modelBuilder.Entity<Product>());
        ConfigureCartItem(modelBuilder.Entity<CartItem>());
    }


    /// <summary>
    /// Product 的 Fluent API 設定。
    /// </summary>
    private static void ConfigureProduct(
        EntityTypeBuilder<Product> entity)
    {
        // ------------------------------------------
        // Primary Key
        // ------------------------------------------

        entity.HasKey(product => product.Id);


        // ------------------------------------------
        // Columns
        // ------------------------------------------

        entity.Property(product => product.Title)
            .IsRequired()
            .HasMaxLength(120);

        entity.Property(product => product.Description)
            .IsRequired()
            .HasMaxLength(2000);

        entity.Property(product => product.Brand)
            .IsRequired()
            .HasMaxLength(120);

        entity.Property(product => product.Category)
            .IsRequired()
            .HasMaxLength(120);

        entity.Property(product => product.ThumbnailUrl)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(product => product.ImageUrlsJson)
            .IsRequired();


        // ------------------------------------------
        // Index
        // ------------------------------------------

        /*
            ExternalId 是 DummyJSON 的商品 ID。

            同一筆 DummyJSON 商品不應該被匯入兩次。

            所以建立 Unique Index：

            ExternalId = 1
            ExternalId = 1   ← 不允許

            但 ExternalId 是 nullable，
            因為後台手動建立的商品可以沒有 ExternalId。
        */
        entity.HasIndex(product => product.ExternalId)
            .IsUnique();


        /*
            Step 3 / Step 5 會經常依 Category 篩選商品：

            WHERE Category = 'beauty'

            因此先替 Category 建立 index。
        */
        entity.HasIndex(product => product.Category);


        /*
            首頁之後會查詢精選商品。
        */
        entity.HasIndex(product => product.IsFeatured);
    }


    /// <summary>
    /// CartItem 的 Fluent API 設定。
    /// </summary>
    private static void ConfigureCartItem(
        EntityTypeBuilder<CartItem> entity)
    {
        // ------------------------------------------
        // Primary Key
        // ------------------------------------------

        entity.HasKey(item => item.Id);


        // ------------------------------------------
        // Columns
        // ------------------------------------------

        entity.Property(item => item.CartKey)
            .IsRequired()
            .HasMaxLength(100);


        // ------------------------------------------
        // Relationship
        // ------------------------------------------

        entity.HasOne(item => item.Product)
            /*
                Product class 裡目前沒有：

                ICollection<CartItem> CartItems

                所以 WithMany() 不指定 navigation property。

                邏輯仍然是一對多：

                Product
                    ↓
                很多 CartItem
            */
            .WithMany()

            /*
                CartItem.ProductId 是 Foreign Key。
            */
            .HasForeignKey(item => item.ProductId)

            /*
                如果 Product 被刪除，
                與它相關的 CartItem 一起刪除。

                Product 5 DELETE
                      ↓
                CartItem(ProductId = 5) DELETE
            */
            .OnDelete(DeleteBehavior.Cascade);


        // ------------------------------------------
        // Composite Unique Index
        // ------------------------------------------

        /*
            同一個購物車裡，
            同一商品只保留一筆 CartItem。

            例如：

            CartKey    ProductId    Quantity
            abc           5            3

            而不是：

            abc           5            1
            abc           5            1
            abc           5            1

            Step 6 加入相同商品時，
            我們會更新 Quantity。
        */
        entity.HasIndex(item => new
        {
            item.CartKey,
            item.ProductId
        })
            .IsUnique();
    }
}
