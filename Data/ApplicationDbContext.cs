using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;

namespace Nexus.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Category> Categories => Set<Category>();

        public DbSet<Product> Products => Set<Product>();

        public DbSet<ProductImage> ProductImages => Set<ProductImage>();

        public DbSet<ProductOption> ProductOptions => Set<ProductOption>();

        public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();

        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

        public DbSet<VariantOptionValue> VariantOptionValues => Set<VariantOptionValue>();

        public DbSet<CartItem> CartItems => Set<CartItem>();

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Category>(entity =>
            {
                entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
                entity.Property(c => c.Slug).HasMaxLength(200).IsRequired();
                entity.Property(c => c.Description).HasMaxLength(2000);
                entity.Property(c => c.ImageUrl).HasMaxLength(500);

                entity.HasIndex(c => c.Slug).IsUnique();
            });

            builder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.Slug).HasMaxLength(200).IsRequired();
                entity.Property(p => p.Description).HasMaxLength(4000);

                entity.HasIndex(p => p.Slug).IsUnique();

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ProductImage>(entity =>
            {
                entity.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
                entity.Property(i => i.AltText).HasMaxLength(200);

                entity.HasIndex(i => i.ProductId);

                entity.HasOne(i => i.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProductOption>(entity =>
            {
                entity.Property(o => o.Name).HasMaxLength(100).IsRequired();

                entity.HasIndex(o => o.ProductId);

                entity.HasOne(o => o.Product)
                    .WithMany(p => p.Options)
                    .HasForeignKey(o => o.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProductOptionValue>(entity =>
            {
                entity.Property(v => v.Value).HasMaxLength(100).IsRequired();

                entity.HasIndex(v => v.ProductOptionId);

                entity.HasOne(v => v.ProductOption)
                    .WithMany(o => o.Values)
                    .HasForeignKey(v => v.ProductOptionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProductVariant>(entity =>
            {
                entity.Property(v => v.Sku).HasMaxLength(50).IsRequired();
                entity.Property(v => v.Price).HasPrecision(18, 2);
                entity.Property(v => v.ImageUrl).HasMaxLength(500);

                entity.HasIndex(v => v.Sku).IsUnique();
                entity.HasIndex(v => v.ProductId);

                entity.HasOne(v => v.Product)
                    .WithMany(p => p.Variants)
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<VariantOptionValue>(entity =>
            {
                entity.HasKey(v => new { v.ProductVariantId, v.ProductOptionValueId });

                entity.HasIndex(v => v.ProductVariantId);

                entity.HasOne(v => v.ProductVariant)
                    .WithMany(pv => pv.OptionValues)
                    .HasForeignKey(v => v.ProductVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.ProductOptionValue)
                    .WithMany(ov => ov.VariantLinks)
                    .HasForeignKey(v => v.ProductOptionValueId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CartItem>(entity =>
            {
                entity.Property(ci => ci.UserId).HasMaxLength(450).IsRequired();

                entity.HasIndex(ci => ci.UserId);
                entity.HasIndex(ci => new { ci.UserId, ci.ProductVariantId }).IsUnique();

                entity.HasOne(ci => ci.ProductVariant)
                    .WithMany()
                    .HasForeignKey(ci => ci.ProductVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(ci => ci.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Order>(entity =>
            {
                entity.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
                entity.Property(o => o.UserId).HasMaxLength(450).IsRequired();

                entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
                entity.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
                entity.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFee).HasPrecision(18, 2);
                entity.Property(o => o.TaxAmount).HasPrecision(18, 2);
                entity.Property(o => o.Total).HasPrecision(18, 2);
                entity.Property(o => o.Currency).HasMaxLength(3).IsRequired();

                entity.Property(o => o.ShipFullName).HasMaxLength(200).IsRequired();
                entity.Property(o => o.ShipPhone).HasMaxLength(40).IsRequired();
                entity.Property(o => o.ShipStreet).HasMaxLength(300).IsRequired();
                entity.Property(o => o.ShipCity).HasMaxLength(120).IsRequired();
                entity.Property(o => o.ShipState).HasMaxLength(120);
                entity.Property(o => o.ShipPostalCode).HasMaxLength(20);
                entity.Property(o => o.ShipCountry).HasMaxLength(120).IsRequired();

                entity.HasIndex(o => o.OrderNumber).IsUnique();
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.Status);

                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OrderItem>(entity =>
            {
                entity.Property(oi => oi.ProductName).HasMaxLength(200).IsRequired();
                entity.Property(oi => oi.VariantLabel).HasMaxLength(300);
                entity.Property(oi => oi.Sku).HasMaxLength(50).IsRequired();
                entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
                entity.Property(oi => oi.LineTotal).HasPrecision(18, 2);

                entity.HasIndex(oi => oi.OrderId);

                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.Items)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.ProductVariant)
                    .WithMany()
                    .HasForeignKey(oi => oi.ProductVariantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
                entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
                entity.Property(p => p.Amount).HasPrecision(18, 2);
                entity.Property(p => p.Currency).HasMaxLength(3).IsRequired();
                entity.Property(p => p.GatewayTransactionRef).HasMaxLength(100);

                entity.HasIndex(p => p.OrderId);
                entity.HasIndex(p => p.GatewayTransactionRef)
                    .IsUnique()
                    .HasFilter("[GatewayTransactionRef] IS NOT NULL");

                entity.HasOne(p => p.Order)
                    .WithMany(o => o.Payments)
                    .HasForeignKey(p => p.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OrderEvent>(entity =>
            {
                entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
                entity.Property(e => e.OldStatus).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.NewStatus).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.Message).HasMaxLength(1000);
                entity.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();

                entity.HasIndex(e => e.OrderId);

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.Events)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
