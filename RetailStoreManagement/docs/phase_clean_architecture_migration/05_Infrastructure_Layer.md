# 05. Infrastructure Layer - Chi Tiết Triển Khai

## 1. Tổng Quan

**Infrastructure Layer** chứa các implementation cụ thể:
- **GenericRepository**: EF Core implementation của IGenericRepository
- **UnitOfWork**: Transaction management
- **DbContext**: Entity Framework Core context
- **Services**: External services (Auth, Token, etc.)

## 2. Cấu Trúc Thư Mục

```
src/Infrastructure/
├── Infrastructure.csproj
├── DependencyInjection.cs
├── Database/
│   ├── ApplicationDbContext.cs
│   └── Configurations/
│       ├── UserConfiguration.cs
│       ├── ProductConfiguration.cs
│       ├── OrderConfiguration.cs
│       └── ...
├── SeedWork/
│   ├── GenericRepository.cs
│   └── UnitOfWork.cs
└── Services/
    ├── AuthService.cs
    ├── TokenService.cs
    └── ...
```

## 3. Project File

```xml
<!-- src/Infrastructure/Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Domain\Domain.csproj" />
        <ProjectReference Include="..\Application\Application.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.9" />
        <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.2" />
        <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.9" />
        <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    </ItemGroup>

</Project>
```

## 4. SeedWork - Repository Pattern

### 4.1 GenericRepository

```csharp
// File: src/Infrastructure/SeedWork/GenericRepository.cs
using System.Linq.Expressions;
using Domain.SeedWork;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SeedWork
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _dbContext;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(ApplicationDbContext context)
        {
            _dbContext = context;
            _dbSet = _dbContext.Set<T>();
        }

        #region Query Methods

        public T? GetById<TKey>(TKey id)
        {
            return _dbSet.Find(id);
        }

        public async Task<T?> GetAsync<TKey>(TKey id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T?> GetByIdAsync<TKey, TProperty>(
            TKey id, 
            params Expression<Func<T, TProperty>>[] navigationProperties)
        {
            IQueryable<T> query = _dbSet;
            
            foreach (var navigationProperty in navigationProperties)
            {
                query = query.Include(navigationProperty);
            }
            
            return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id));
        }

        public IQueryable<T> GetAll()
        {
            return _dbSet.AsNoTracking();
        }

        public IQueryable<T> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            IEnumerable<string>? includes = null,
            bool noneTracking = true)
        {
            var query = noneTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();

            if (filter is not null)
                query = query.Where(filter);

            if (includes is not null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            if (orderBy is not null)
                query = orderBy(query);

            return query;
        }

        public async Task<IQueryable<T>> FindByAsync(
            Expression<Func<T, bool>> predicate,
            IEnumerable<string>? includes = null,
            bool noneTracking = true)
        {
            var query = noneTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();

            if (includes is not null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await Task.FromResult(query.Where(predicate));
        }

        #endregion

        #region Command Methods

        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(object key, T entity)
        {
            var existingEntity = await _dbSet.FindAsync(key);
            if (existingEntity is not null)
            {
                _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
            }
        }

        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        #endregion

        #region Utility Methods

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public async Task<int> SaveAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        public int Count()
        {
            return _dbSet.Count();
        }

        public async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        #endregion
    }
}
```

### 4.2 UnitOfWork

```csharp
// File: src/Infrastructure/SeedWork/UnitOfWork.cs
using Domain.SeedWork;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.SeedWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<Type, object> _repositories = [];
        private bool _disposed;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IGenericRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);
            
            if (!_repositories.ContainsKey(type))
            {
                var repository = new GenericRepository<T>(_context);
                _repositories.Add(type, repository);
            }
            
            return (IGenericRepository<T>)_repositories[type];
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public IDbContextTransaction BeginTransaction()
        {
            return _context.Database.BeginTransaction();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }
    }
}
```

## 5. Database - DbContext

```csharp
// File: src/Infrastructure/Database/ApplicationDbContext.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
            : base(options)
        {
        }

        public DbSet<UserEntity> Users => Set<UserEntity>();
        public DbSet<ProductEntity> Products => Set<ProductEntity>();
        public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
        public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();
        public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
        public DbSet<OrderEntity> Orders => Set<OrderEntity>();
        public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();
        public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
        public DbSet<InventoryEntity> Inventory => Set<InventoryEntity>();
        public DbSet<InventoryHistoryEntity> InventoryHistories => Set<InventoryHistoryEntity>();
        public DbSet<PromotionEntity> Promotions => Set<PromotionEntity>();
        public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Apply all configurations from assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            
            // Global query filter for soft delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType.GetProperty("IsDeleted") != null)
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, "IsDeleted");
                    var falseConstant = Expression.Constant(false);
                    var condition = Expression.Equal(property, falseConstant);
                    var lambda = Expression.Lambda(condition, parameter);
                    
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity<int>>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }
            
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
```

## 6. Entity Configurations

```csharp
// File: src/Infrastructure/Database/Configurations/ProductConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
    {
        public void Configure(EntityTypeBuilder<ProductEntity> builder)
        {
            builder.ToTable("products");
            
            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);
                
            builder.Property(x => x.Sku)
                .IsRequired()
                .HasMaxLength(50);
                
            builder.Property(x => x.Price)
                .HasPrecision(18, 2);
                
            builder.Property(x => x.CostPrice)
                .HasPrecision(18, 2);
                
            builder.HasIndex(x => x.Sku).IsUnique();
            
            builder.HasOne(x => x.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(x => x.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

```csharp
// File: src/Infrastructure/Database/Configurations/OrderConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
    {
        public void Configure(EntityTypeBuilder<OrderEntity> builder)
        {
            builder.ToTable("orders");
            
            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.OrderNumber)
                .IsRequired()
                .HasMaxLength(50);
                
            builder.Property(x => x.SubTotal)
                .HasPrecision(18, 2);
                
            builder.Property(x => x.DiscountAmount)
                .HasPrecision(18, 2);
                
            builder.Property(x => x.TaxAmount)
                .HasPrecision(18, 2);
                
            builder.Property(x => x.TotalAmount)
                .HasPrecision(18, 2);
                
            builder.HasIndex(x => x.OrderNumber).IsUnique();
            
            builder.HasMany(x => x.OrderItems)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

## 7. Services

### 7.1 IAuthService Interface (Application Layer)

```csharp
// File: src/Application/Features/Auth/Services/IAuthService.cs
using Application.Features.Auth.Dtos;

namespace Application.Features.Auth.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> ValidateUserAsync(string username, string password);
        Task<string> GenerateAccessTokenAsync(int userId);
        Task<string> GenerateRefreshTokenAsync(int userId);
        Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken);
        Task RevokeRefreshTokenAsync(int userId, string refreshToken);
    }
}
```

### 7.2 AuthService Implementation

```csharp
// File: src/Infrastructure/Services/AuthService.cs
using Application.Features.Auth.Dtos;
using Application.Features.Auth.Services;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<LoginResponse?> ValidateUserAsync(string username, string password)
        {
            var user = await _unitOfWork.Repository<UserEntity>()
                .GetAll()
                .FirstOrDefaultAsync(x => x.Username == username && x.IsActive);

            if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return new LoginResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role.ToString()
            };
        }

        public Task<string> GenerateAccessTokenAsync(int userId)
        {
            var user = _unitOfWork.Repository<UserEntity>().GetById<int>(userId);
            if (user is null) return Task.FromResult(string.Empty);

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryMinutes"])),
                signingCredentials: credentials
            );

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        public async Task<string> GenerateRefreshTokenAsync(int userId)
        {
            var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            
            var token = new UserRefreshToken
            {
                UserId = userId,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<UserRefreshToken>().AddAsync(token);
            await _unitOfWork.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken)
        {
            var token = await _unitOfWork.Repository<UserRefreshToken>()
                .GetAll()
                .FirstOrDefaultAsync(x => 
                    x.UserId == userId && 
                    x.Token == refreshToken && 
                    x.ExpiresAt > DateTime.UtcNow &&
                    !x.IsRevoked);

            return token is not null;
        }

        public async Task RevokeRefreshTokenAsync(int userId, string refreshToken)
        {
            var token = await _unitOfWork.Repository<UserRefreshToken>()
                .GetAll()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Token == refreshToken);

            if (token is not null)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}
```

## 8. Dependency Injection

```csharp
// File: src/Infrastructure/DependencyInjection.cs
using Application.Features.Auth.Services;
using Domain.SeedWork;
using Infrastructure.Database;
using Infrastructure.SeedWork;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Database
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Services
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
```

## 9. Migration Checklist

- [ ] Tạo `Infrastructure.csproj`
- [ ] Migrate `ApplicationDbContext` từ `Data/` folder hiện tại
- [ ] Tạo `GenericRepository<T>`
- [ ] Tạo `UnitOfWork`
- [ ] Tạo Entity Configurations cho tất cả entities
- [ ] Migrate `AuthService`
- [ ] Tạo `DependencyInjection.cs`
- [ ] Test database connection

---

*Xem tiếp: [06_WebApi_Layer.md](./06_WebApi_Layer.md)*
