# 04. Application Layer - Chi Tiết Triển Khai

## 1. Tổng Quan

**Application Layer** chứa business logic và use cases của ứng dụng:
- CQRS Pattern (Commands, Queries, Handlers)
- Validators (FluentValidation)
- DTOs (Data Transfer Objects)
- Pipeline Behaviours
- Mapping Profiles

## 2. Cấu Trúc Thư Mục

```
src/Application/
├── Application.csproj
├── DependencyInjection.cs
├── Abstractions/
│   └── Messaging/
│       ├── ICommand.cs
│       ├── ICommandHandler.cs
│       ├── IQuery.cs
│       └── IQueryHandler.cs
├── Common/
│   ├── Behaviours/
│   │   └── ValidationBehaviour.cs
│   ├── Exceptions/
│   │   ├── ValidationException.cs
│   │   ├── NotFoundException.cs
│   │   └── BadRequestException.cs
│   └── Models/
│       ├── ApiResponse.cs
│       ├── PaginatedResponse.cs
│       └── PaginationRequest.cs
├── Features/
│   ├── Auth/
│   │   ├── Commands/
│   │   │   ├── LoginCommand.cs
│   │   │   ├── LogoutCommand.cs
│   │   │   └── RefreshTokenCommand.cs
│   │   ├── Handlers/
│   │   │   ├── LoginCommandHandler.cs
│   │   │   └── ...
│   │   ├── Validators/
│   │   │   └── LoginCommandValidator.cs
│   │   ├── Dtos/
│   │   │   ├── LoginRequest.cs
│   │   │   └── LoginResponse.cs
│   │   └── Services/
│   │       └── IAuthService.cs
│   ├── Products/
│   ├── Categories/
│   ├── Orders/
│   ├── Customers/
│   ├── Suppliers/
│   ├── Inventory/
│   ├── Promotions/
│   ├── Users/
│   └── Reports/
└── Profiles/
    └── MappingProfile.cs
```

## 3. Project File

```xml
<!-- src/Application/Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Domain\Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="MediatR" Version="12.4.0" />
        <PackageReference Include="FluentValidation" Version="11.10.0" />
        <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
        <PackageReference Include="AutoMapper" Version="12.0.1" />
        <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
    </ItemGroup>

</Project>
```

## 4. Abstractions - CQRS Interfaces

### 4.1 ICommand & ICommandHandler

```csharp
// File: src/Application/Abstractions/Messaging/ICommand.cs
using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging
{
    public interface ICommand<TResponse> : IRequest<ApiResponse<TResponse>>
    {
    }
    
    // Command không trả về data (void command)
    public interface ICommand : IRequest<ApiResponse<bool>>
    {
    }
}
```

```csharp
// File: src/Application/Abstractions/Messaging/ICommandHandler.cs
using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging
{
    public interface ICommandHandler<in TCommand, TResponse> 
        : IRequestHandler<TCommand, ApiResponse<TResponse>> 
        where TCommand : ICommand<TResponse>
    {
    }
    
    public interface ICommandHandler<in TCommand> 
        : IRequestHandler<TCommand, ApiResponse<bool>> 
        where TCommand : ICommand
    {
    }
}
```

### 4.2 IQuery & IQueryHandler

```csharp
// File: src/Application/Abstractions/Messaging/IQuery.cs
using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging
{
    public interface IQuery<TResponse> : IRequest<ApiResponse<TResponse>>
    {
    }
}
```

```csharp
// File: src/Application/Abstractions/Messaging/IQueryHandler.cs
using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging
{
    public interface IQueryHandler<in TQuery, TResponse> 
        : IRequestHandler<TQuery, ApiResponse<TResponse>> 
        where TQuery : IQuery<TResponse>
    {
    }
}
```

## 5. Common Models

### 5.1 ApiResponse

```csharp
// File: src/Application/Common/Models/ApiResponse.cs
namespace Application.Common.Models
{
    public class ApiResponse<T>
    {
        public bool Succeeded { get; set; } = true;
        public int ResponseCode { get; set; } = 200;
        public string? Message { get; set; }
        public T? Data { get; set; }
        public IDictionary<string, string[]>? Errors { get; set; }

        public ApiResponse() { }

        public ApiResponse(T? data, bool succeeded = true, int code = 200, string? message = null)
        {
            Data = data;
            Succeeded = succeeded;
            ResponseCode = code;
            Message = message;
        }

        public static ApiResponse<T> Success(T? data = default, string? message = null) 
            => new(data, true, 200, message);

        public static ApiResponse<T> Failure(string? message = null, int code = 400) 
            => new(default, false, code, message);

        public static ApiResponse<T> NotFound(string? message = "Resource not found") 
            => new(default, false, 404, message);
    }
}
```

### 5.2 PaginatedResponse

```csharp
// File: src/Application/Common/Models/PaginatedResponse.cs
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Models
{
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = [];
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public static async Task<PaginatedResponse<T>> CreateAsync(
            IQueryable<T> source, 
            int pageIndex, 
            int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<T>
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = count
            };
        }
    }
}
```

### 5.3 PaginationRequest

```csharp
// File: src/Application/Common/Models/PaginationRequest.cs
using Application.Abstractions.Messaging;

namespace Application.Common.Models
{
    public class PaginationRequest<TResponse> : IQuery<TResponse>
    {
        public string? SearchTerm { get; set; }
        public string? SortField { get; set; }
        public string? SortOrder { get; set; } = "asc";
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
```

## 6. Exceptions

```csharp
// File: src/Application/Common/Exceptions/ValidationException.cs
using FluentValidation.Results;

namespace Application.Common.Exceptions
{
    public class ValidationException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }

        public ValidationException() : base("One or more validation failures have occurred.")
        {
            Errors = new Dictionary<string, string[]>();
        }

        public ValidationException(IEnumerable<ValidationFailure> failures) : this()
        {
            Errors = failures
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                .ToDictionary(g => g.Key, g => g.ToArray());
        }
    }
}
```

```csharp
// File: src/Application/Common/Exceptions/NotFoundException.cs
namespace Application.Common.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException() : base() { }
        
        public NotFoundException(string message) : base(message) { }
        
        public NotFoundException(string name, object key)
            : base($"Entity \"{name}\" ({key}) was not found.") { }
    }
}
```

```csharp
// File: src/Application/Common/Exceptions/BadRequestException.cs
namespace Application.Common.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException() : base() { }
        
        public BadRequestException(string message) : base(message) { }
    }
}
```

## 7. ValidationBehaviour

```csharp
// File: src/Application/Common/Behaviours/ValidationBehaviour.cs
using Application.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace Application.Common.Behaviours
{
    public class ValidationBehaviour<TRequest, TResponse> 
        : IPipelineBehavior<TRequest, TResponse> 
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        
        public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }
        
        public async Task<TResponse> Handle(
            TRequest request, 
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);

                var validationResults = await Task.WhenAll(
                    _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

                var failures = validationResults
                    .Where(r => !r.IsValid)
                    .SelectMany(r => r.Errors)
                    .ToList();

                if (failures.Count > 0)
                    throw new ValidationException(failures);
            }
            
            return await next();
        }
    }
}
```

## 8. Feature Example - Products

### 8.1 Commands

```csharp
// File: src/Application/Features/Products/Commands/CreateProductCommand.cs
using Application.Abstractions.Messaging;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Commands
{
    public record CreateProductCommand(
        string Name,
        string Sku,
        decimal Price,
        int CategoryId,
        int SupplierId,
        string? Description = null,
        string? ImageUrl = null
    ) : ICommand<ProductDto>;
}
```

```csharp
// File: src/Application/Features/Products/Commands/UpdateProductCommand.cs
using Application.Abstractions.Messaging;

namespace Application.Features.Products.Commands
{
    public record UpdateProductCommand(
        int Id,
        string Name,
        string Sku,
        decimal Price,
        int CategoryId,
        int SupplierId,
        string? Description = null,
        string? ImageUrl = null,
        bool IsActive = true
    ) : ICommand<bool>;
}
```

```csharp
// File: src/Application/Features/Products/Commands/DeleteProductCommand.cs
using Application.Abstractions.Messaging;

namespace Application.Features.Products.Commands
{
    public record DeleteProductCommand(int Id) : ICommand;
}
```

### 8.2 Queries

```csharp
// File: src/Application/Features/Products/Queries/GetProductsQuery.cs
using Application.Common.Models;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Queries
{
    public class GetProductsQuery : PaginationRequest<PaginatedResponse<ProductDto>>
    {
        public int? CategoryId { get; set; }
        public int? SupplierId { get; set; }
        public bool? IsActive { get; set; }
    }
}
```

```csharp
// File: src/Application/Features/Products/Queries/GetProductByIdQuery.cs
using Application.Abstractions.Messaging;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Queries
{
    public record GetProductByIdQuery(int Id) : IQuery<ProductDto>;
}
```

### 8.3 DTOs

```csharp
// File: src/Application/Features/Products/Dtos/ProductDto.cs
namespace Application.Features.Products.Dtos
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        
        // Related data
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        
        // Inventory info
        public int? StockQuantity { get; set; }
    }
}
```

### 8.4 Validators

```csharp
// File: src/Application/Features/Products/Validators/CreateProductCommandValidator.cs
using Application.Features.Products.Commands;
using FluentValidation;

namespace Application.Features.Products.Validators
{
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
                .MaximumLength(200).WithMessage("Tên sản phẩm không được vượt quá 200 ký tự");

            RuleFor(x => x.Sku)
                .NotEmpty().WithMessage("Mã SKU không được để trống")
                .MaximumLength(50);

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá phải lớn hơn 0");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Vui lòng chọn danh mục");

            RuleFor(x => x.SupplierId)
                .GreaterThan(0).WithMessage("Vui lòng chọn nhà cung cấp");
        }
    }
}
```

### 8.5 Handlers

```csharp
// File: src/Application/Features/Products/Handlers/CreateProductCommandHandler.cs
using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Products.Commands;
using Application.Features.Products.Dtos;
using AutoMapper;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Products.Handlers
{
    public class CreateProductCommandHandler(
        IUnitOfWork unitOfWork, 
        IMapper mapper) 
        : ICommandHandler<CreateProductCommand, ProductDto>
    {
        public async Task<ApiResponse<ProductDto>> Handle(
            CreateProductCommand request, 
            CancellationToken cancellationToken)
        {
            var product = new ProductEntity
            {
                Name = request.Name,
                Sku = request.Sku,
                Price = request.Price,
                CategoryId = request.CategoryId,
                SupplierId = request.SupplierId,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                IsActive = true
            };
            
            await unitOfWork.Repository<ProductEntity>().AddAsync(product);
            await unitOfWork.SaveChangesAsync();
            
            var dto = mapper.Map<ProductDto>(product);
            return ApiResponse<ProductDto>.Success(dto, "Tạo sản phẩm thành công");
        }
    }
}
```

```csharp
// File: src/Application/Features/Products/Handlers/GetProductsQueryHandler.cs
using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Products.Dtos;
using Application.Features.Products.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Handlers
{
    public class GetProductsQueryHandler(IUnitOfWork unitOfWork) 
        : IQueryHandler<GetProductsQuery, PaginatedResponse<ProductDto>>
    {
        public async Task<ApiResponse<PaginatedResponse<ProductDto>>> Handle(
            GetProductsQuery request, 
            CancellationToken cancellationToken)
        {
            var query = unitOfWork.Repository<ProductEntity>()
                .GetAll()
                .Include(x => x.Category)
                .Include(x => x.Supplier)
                .Include(x => x.Inventory)
                .AsNoTracking();

            // Apply filters
            if (request.CategoryId.HasValue)
                query = query.Where(x => x.CategoryId == request.CategoryId);

            if (request.SupplierId.HasValue)
                query = query.Where(x => x.SupplierId == request.SupplierId);

            if (request.IsActive.HasValue)
                query = query.Where(x => x.IsActive == request.IsActive);

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(x => 
                    x.Name.ToLower().Contains(term) || 
                    x.Sku.ToLower().Contains(term));
            }

            // Apply sorting
            query = request.SortField?.ToLower() switch
            {
                "name" => request.SortOrder == "desc" 
                    ? query.OrderByDescending(x => x.Name) 
                    : query.OrderBy(x => x.Name),
                "price" => request.SortOrder == "desc" 
                    ? query.OrderByDescending(x => x.Price) 
                    : query.OrderBy(x => x.Price),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            // Project to DTO
            var dtoQuery = query.Select(x => new ProductDto
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Description = x.Description,
                Price = x.Price,
                CostPrice = x.CostPrice,
                ImageUrl = x.ImageUrl,
                IsActive = x.IsActive,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.Name,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier.Name,
                StockQuantity = x.Inventory != null ? x.Inventory.Quantity : 0
            });

            var response = await PaginatedResponse<ProductDto>.CreateAsync(
                dtoQuery, request.PageIndex, request.PageSize);
                
            return ApiResponse<PaginatedResponse<ProductDto>>.Success(response);
        }
    }
}
```

## 9. Dependency Injection

```csharp
// File: src/Application/DependencyInjection.cs
using Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection).Assembly;
            
            // AutoMapper
            services.AddAutoMapper(assembly);
            
            // FluentValidation - auto-register all validators
            services.AddValidatorsFromAssembly(assembly);
            
            // MediatR - auto-register all handlers
            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(assembly);
                config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            });

            return services;
        }
    }
}
```

## 10. Module Statistics

| Module | Commands | Queries | Handlers | Validators | DTOs |
|--------|----------|---------|----------|------------|------|
| Auth | 4 | 1 | 5 | 3 | 4 |
| Users | 3 | 2 | 5 | 3 | 3 |
| Products | 3 | 3 | 6 | 3 | 4 |
| Categories | 3 | 2 | 5 | 3 | 3 |
| Orders | 7 | 3 | 10 | 5 | 8 |
| Customers | 3 | 2 | 5 | 3 | 3 |
| Suppliers | 3 | 2 | 5 | 3 | 3 |
| Inventory | 3 | 2 | 5 | 3 | 4 |
| Promotions | 4 | 2 | 6 | 4 | 4 |
| Reports | 0 | 5 | 5 | 2 | 5 |
| **TOTAL** | **33** | **24** | **57** | **32** | **41** |

---

*Xem tiếp: [05_Infrastructure_Layer.md](./05_Infrastructure_Layer.md)*
