using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Products.Dtos;
using Application.Features.Products.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Handlers;

public class GetProductsByIdsQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetProductsByIdsQuery, List<ProductDto>>
{
    public async Task<ApiResponse<List<ProductDto>>> Handle(
        GetProductsByIdsQuery request, 
        CancellationToken cancellationToken)
    {
        var products = await unitOfWork.Repository<ProductEntity>()
            .GetAllReadOnly()
            .Where(x => request.Ids.Contains(x.Id) && !x.DeletedAt.HasValue)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                ProductName = x.ProductName,
                Barcode = x.Barcode,
                Price = x.Price,
                Unit = x.Unit,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.CategoryName,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier.Name,
                StockQuantity = x.Inventory != null ? x.Inventory.Quantity : 0,
                CreatedAt = x.CreatedAt
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return ApiResponse<List<ProductDto>>.Success(products);
    }
}
