using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("Users");
        
        // Primary key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        
        // Base entity properties (PascalCase)
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        // Entity-specific properties (snake_case)
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Password).HasColumnName("password").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(100);
        builder.Property(x => x.Role).HasColumnName("role").IsRequired();
    }
}

public class ProductEntityConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Price).HasColumnName("price").IsRequired();
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20);
        builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(1024);
        builder.Property(x => x.ImageFileId).HasColumnName("image_file_id").HasMaxLength(255);
        
        builder.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.Supplier).WithMany(s => s.Products).HasForeignKey(x => x.SupplierId);
    }
}

public class CategoryEntityConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        builder.ToTable("Categories");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.CategoryName).HasColumnName("category_name").HasMaxLength(100).IsRequired();
    }
}

public class CustomerEntityConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
    }
}

public class SupplierEntityConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> builder)
    {
        builder.ToTable("Suppliers");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
    }
}

public class OrderEntityConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("Orders");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.PromoId).HasColumnName("promo_id");
        builder.Property(x => x.OrderDate).HasColumnName("order_date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").IsRequired();
        
        builder.HasOne(x => x.Customer).WithMany(c => c.Orders).HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.User).WithMany(u => u.Orders).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Promotion).WithMany(p => p.Orders).HasForeignKey(x => x.PromoId);
    }
}

public class OrderItemEntityConfiguration : IEntityTypeConfiguration<OrderItemEntity>
{
    public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
    {
        builder.ToTable("OrderItems");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.Price).HasColumnName("price").IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").IsRequired();
        
        builder.HasOne(x => x.Order).WithMany(o => o.OrderItems).HasForeignKey(x => x.OrderId);
        builder.HasOne(x => x.Product).WithMany(p => p.OrderItems).HasForeignKey(x => x.ProductId);
    }
}

public class PaymentEntityConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").IsRequired();
        builder.Property(x => x.PaymentDate).HasColumnName("payment_date").IsRequired();
        
        builder.HasOne(x => x.Order).WithMany(o => o.Payments).HasForeignKey(x => x.OrderId);
    }
}

public class PromotionEntityConfiguration : IEntityTypeConfiguration<PromotionEntity>
{
    public void Configure(EntityTypeBuilder<PromotionEntity> builder)
    {
        builder.ToTable("Promotions");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.PromoCode).HasColumnName("promo_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
        builder.Property(x => x.DiscountType).HasColumnName("discount_type").IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnName("discount_value").IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(x => x.MinOrderAmount).HasColumnName("min_order_amount").IsRequired();
        builder.Property(x => x.UsageLimit).HasColumnName("usage_limit").IsRequired();
        builder.Property(x => x.UsedCount).HasColumnName("used_count").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
    }
}

public class InventoryEntityConfiguration : IEntityTypeConfiguration<InventoryEntity>
{
    public void Configure(EntityTypeBuilder<InventoryEntity> builder)
    {
        builder.ToTable("Inventory");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        
        builder.HasOne(x => x.Product).WithOne(p => p.Inventory).HasForeignKey<InventoryEntity>(x => x.ProductId);
    }
}

public class InventoryHistoryEntityConfiguration : IEntityTypeConfiguration<InventoryHistoryEntity>
{
    public void Configure(EntityTypeBuilder<InventoryHistoryEntity> builder)
    {
        builder.ToTable("InventoryHistories");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.QuantityChange).HasColumnName("quantity_change").IsRequired();
        builder.Property(x => x.QuantityAfter).HasColumnName("quantity_after").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(255).IsRequired();
        
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}

public class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        // UserRefreshTokens có mixed naming: created_at là snake_case, UpdatedAt/DeletedAt là PascalCase
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
        
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.IsRevoked).HasColumnName("is_revoked").IsRequired();
        
        builder.HasOne(x => x.User).WithMany(u => u.UserRefreshTokens).HasForeignKey(x => x.UserId);
    }
}
