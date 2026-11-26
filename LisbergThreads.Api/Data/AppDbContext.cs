using LisbergThreads.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {

        }

        public DbSet<ContentText> ContentTexts => Set<ContentText>();

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<MobilePayPayment> MobilePayPayments => Set<MobilePayPayment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<MobilePayPayment>(e =>
            {
                e.Property(p => p.Id).ValueGeneratedNever();
                e.Property(p => p.Total).HasColumnType("decimal(18,2)");
                e.Property(p => p.State).HasMaxLength(50);
                e.Property(p => p.RedirectUrl).HasMaxLength(2048);
                e.Property(p => p.RowVersion).IsRowVersion();
                e.HasIndex(p => p.CartId).IsUnique()
                    .HasFilter("[CartId] IS NOT NULL AND [IsActive] = 1");
                e.HasIndex(p => p.OrderId).IsUnique()
                    .HasFilter("[OrderId] IS NOT NULL");
                e.HasIndex(p => new { p.IsActive, p.NextCheckAt });
            });

            // Products
            builder.Entity<Product>(e =>
            {
                e.Property(p => p.Id).HasDefaultValueSql("NEWID()");
                e.Property(p => p.Price).HasColumnType("decimal(18,2)");
                e.Property(p => p.RowVersion).IsRowVersion();
                e.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(p => p.UpdatedAt).HasColumnType("datetime2");
                e.HasIndex(p => p.Name).HasDatabaseName("IX_Products_Name");
                e.HasIndex(p => p.IsActive).HasDatabaseName("IX_Products_IsActive");
            });
            
            // ProductImages
            builder.Entity<ProductImage>(e =>
            {
                e.ToTable("ProductImages");

                e.HasKey(i => i.Id);

                e.Property(i => i.ImageUrl)
                    .HasMaxLength(500)
                    .IsRequired();

                e.HasIndex(i => new { i.ProductId, i.SortOrder });

                e.HasOne(i => i.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Orders
            builder.Entity<Order>(e =>
            {
                e.Property(o => o.RowVersion).IsRowVersion();
                e.Property(o => o.Id)
                    .HasDefaultValueSql("NEWID()");

                e.Property(o => o.ConfirmationToken)
                    .HasDefaultValueSql("NEWID()")
                    .IsRequired();

                e.HasIndex(o => o.ConfirmationToken)
                    .IsUnique()
                    .HasDatabaseName("IX_Orders_ConfirmationToken");

                e.Property(o => o.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                e.Property(o => o.Total)
                    .HasColumnType("decimal(18,2)");

                e.Property(o => o.ShippingPrice).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                e.Property(o => o.ShippingMethodId).HasMaxLength(50);
                e.Property(o => o.ShippingMethodName).HasMaxLength(100);
                e.Property(o => o.ShippingCarrier).HasMaxLength(50);
                e.Property(o => o.ServicePointId).HasMaxLength(100);
                e.Property(o => o.ServicePointName).HasMaxLength(254);
                e.Property(o => o.ServicePointAddress).HasMaxLength(254);
                e.Property(o => o.ServicePointAddress2).HasMaxLength(254);
                e.Property(o => o.ServicePointPostalCode).HasMaxLength(10);
                e.Property(o => o.ServicePointCity).HasMaxLength(254);
                e.Property(o => o.TrackingNumber).HasMaxLength(100);

                e.Property(o => o.Status)
                    .HasMaxLength(50);

                e.HasIndex(o => o.UserId)
                    .HasDatabaseName("IX_Orders_UserId");

                e.HasIndex(o => o.Status)
                    .HasDatabaseName("IX_Orders_Status");

                e.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasMany(o => o.OrderItems)
                    .WithOne()
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // OrderItems
            builder.Entity<OrderItem>(e =>
            {
                e.Property(oi => oi.Id).HasDefaultValueSql("NEWID()");
                e.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(oi => oi.LineTotal)
                    .HasColumnType("decimal(18,2)")
                    .HasComputedColumnSql("CONVERT(DECIMAL(18,2), [Quantity] * [UnitPrice])", stored: true);
                e.HasIndex(oi => oi.OrderId).HasDatabaseName("IX_OrderItems_OrderId");
                e.HasIndex(oi => oi.ProductId).HasDatabaseName("IX_OrderItems_ProductId");
            });
        }
    }
}
