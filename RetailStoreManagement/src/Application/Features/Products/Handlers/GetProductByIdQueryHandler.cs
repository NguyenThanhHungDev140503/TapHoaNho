using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Products.Dtos;
using Application.Features.Products.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Handlers;

/// <summary>
/// Handler cho GetProductByIdQuery - lấy chi tiết sản phẩm theo ID
/// </summary>
public class GetProductByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<ApiResponse<ProductDto>> Handle(
        GetProductByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Repository<ProductEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                ProductName = x.ProductName,
                Barcode = x.Barcode,
                Price = x.Price,
                Unit = x.Unit,
                ImageUrl = x.ImageUrl,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.CategoryName,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier.Name,
                StockQuantity = x.Inventory != null ? x.Inventory.Quantity : 0,
                CreatedAt = x.CreatedAt,
                IsDeleted = x.DeletedAt.HasValue
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            throw new NotFoundException("Sản phẩm", request.Id);

        return ApiResponse<ProductDto>.Success(product);
    }
}
