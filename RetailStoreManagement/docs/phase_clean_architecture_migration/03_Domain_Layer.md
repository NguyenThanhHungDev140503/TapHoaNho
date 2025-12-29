# 03. Domain Layer - Chi Tiết Triển Khai

## 1. Tổng Quan

**Domain Layer** là trái tim của ứng dụng, chứa:
- Business entities
- Enumerations
- Repository interfaces (contracts)
- Value Objects (nếu cần)

> [!IMPORTANT]
> Domain Layer **KHÔNG** được phép reference bất kỳ layer nào khác trong solution.

## 2. Cấu Trúc Thư Mục

```
src/Domain/
├── Domain.csproj
├── Common/
│   └── AppSettings.cs
├── Entities/
│   ├── BaseEntity.cs
│   ├── UserEntity.cs
│   ├── ProductEntity.cs
│   ├── CategoryEntity.cs
│   ├── CustomerEntity.cs
│   ├── SupplierEntity.cs
│   ├── OrderEntity.cs
│   ├── OrderItemEntity.cs
│   ├── PaymentEntity.cs
│   ├── InventoryEntity.cs
│   ├── InventoryHistoryEntity.cs
│   ├── PromotionEntity.cs
│   └── UserRefreshToken.cs
├── Enums/
│   ├── OrderStatus.cs
│   ├── PaymentStatus.cs
│   ├── PaymentMethod.cs
│   ├── InventoryTransactionType.cs
│   └── UserRole.cs
└── SeedWork/
    ├── IGenericRepository.cs
    └── IUnitOfWork.cs
```

## 3. Project File

```xml
<!-- src/Domain/Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <!-- NO external dependencies - pure domain -->
    <ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore.Abstractions" Version="9.0.9" />
    </ItemGroup>

</Project>
```

## 4. SeedWork - Generic Patterns

### 4.1 IGenericRepository

```csharp
// File: src/Domain/SeedWork/IGenericRepository.cs
using System.Linq.Expressions;

namespace Domain.SeedWork
{
    public interface IGenericRepository<T> where T : class
    {
        // Query methods
        T? GetById<TKey>(TKey id);
        Task<T?> GetAsync<TKey>(TKey id);
        Task<T?> GetByIdAsync<TKey, TProperty>(
            TKey id, 
            params Expression<Func<T, TProperty>>[] navigationProperties);
        
        IQueryable<T> GetAll();
        IQueryable<T> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            IEnumerable<string>? includes = null,
            bool noneTracking = true);
        
        Task<IQueryable<T>> FindByAsync(
            Expression<Func<T, bool>> predicate,
            IEnumerable<string>? includes = null,
            bool noneTracking = true);

        // Command methods
        Task<T> AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task UpdateAsync(object key, T entity);
        Task DeleteAsync(T entity);
        Task DeleteRangeAsync(IEnumerable<T> entities);

        // Utility methods
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task<int> SaveAsync();
        int Count();
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    }
}
```

### 4.2 IUnitOfWork

```csharp
// File: src/Domain/SeedWork/IUnitOfWork.cs
using Microsoft.EntityFrameworkCore.Storage;

namespace Domain.SeedWork
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<T> Repository<T>() where T : class;
        
        int SaveChanges();
        Task<int> SaveChangesAsync();
        
        IDbContextTransaction BeginTransaction();
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
```

## 5. Entities

### 5.1 BaseEntity

```csharp
// File: src/Domain/Entities/BaseEntity.cs
namespace Domain.Entities
{
    public abstract class BaseEntity<TKey>
    {
        public TKey Id { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
```

### 5.2 UserEntity (Migrate từ hiện tại)

```csharp
// File: src/Domain/Entities/UserEntity.cs
using Domain.Enums;

namespace Domain.Entities
{
    public class UserEntity : BaseEntity<int>
    {
        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public required string Email { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public UserRole Role { get; set; } = UserRole.Staff;
        public bool IsActive { get; set; } = true;
        
        // Navigation
        public virtual ICollection<UserRefreshToken> RefreshTokens { get; set; } = [];
        public virtual ICollection<OrderEntity> Orders { get; set; } = [];
    }
}
```

### 5.3 ProductEntity (Migrate từ hiện tại)

```csharp
// File: src/Domain/Entities/ProductEntity.cs
namespace Domain.Entities
{
    public class ProductEntity : BaseEntity<int>
    {
        public required string Name { get; set; }
        public required string Sku { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string? Barcode { get; set; }
        public string? Unit { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Foreign Keys
        public int CategoryId { get; set; }
        public int SupplierId { get; set; }
        
        // Navigation
        public virtual CategoryEntity Category { get; set; } = null!;
        public virtual SupplierEntity Supplier { get; set; } = null!;
        public virtual InventoryEntity? Inventory { get; set; }
        public virtual ICollection<OrderItemEntity> OrderItems { get; set; } = [];
    }
}
```

### 5.4 OrderEntity (Migrate từ hiện tại)

```csharp
// File: src/Domain/Entities/OrderEntity.cs
using Domain.Enums;

namespace Domain.Entities
{
    public class OrderEntity : BaseEntity<int>
    {
        public required string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        
        public string? Notes { get; set; }
        
        // Foreign Keys
        public int? CustomerId { get; set; }
        public int? UserId { get; set; }
        public int? PromotionId { get; set; }
        
        // Navigation
        public virtual CustomerEntity? Customer { get; set; }
        public virtual UserEntity? User { get; set; }
        public virtual PromotionEntity? Promotion { get; set; }
        public virtual ICollection<OrderItemEntity> OrderItems { get; set; } = [];
        public virtual ICollection<PaymentEntity> Payments { get; set; } = [];
    }
}
```

## 6. Enums

```csharp
// File: src/Domain/Enums/OrderStatus.cs
namespace Domain.Enums
{
    public enum OrderStatus
    {
        Pending = 0,
        Confirmed = 1,
        Processing = 2,
        Shipped = 3,
        Delivered = 4,
        Completed = 5,
        Cancelled = 6,
        Refunded = 7
    }
}
```

```csharp
// File: src/Domain/Enums/UserRole.cs
namespace Domain.Enums
{
    public enum UserRole
    {
        Admin = 0,
        Manager = 1,
        Staff = 2
    }
}
```

## 7. Migration Checklist

- [ ] Tạo `Domain.csproj`
- [ ] Tạo `SeedWork/IGenericRepository.cs`
- [ ] Tạo `SeedWork/IUnitOfWork.cs`
- [ ] Migrate `BaseEntity.cs`
- [ ] Migrate tất cả 12 entities từ folder Entities hiện tại
- [ ] Migrate tất cả 5 enums từ folder Enums hiện tại
- [ ] Tạo `Common/AppSettings.cs`
- [ ] Verify build thành công

---

*Xem tiếp: [04_Application_Layer.md](./04_Application_Layer.md)*
