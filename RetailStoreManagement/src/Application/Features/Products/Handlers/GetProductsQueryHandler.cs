using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Products.Dtos;
using Application.Features.Products.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Handlers;

/// <summary>
/// Handler cho GetProductsQuery - lấy danh sách sản phẩm với pagination
/// </summary>
public class GetProductsQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetProductsQuery, PaginatedResponse<ProductDto>>
{
    public async Task<ApiResponse<PaginatedResponse<ProductDto>>> Handle(
        GetProductsQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<ProductEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        // Apply filters
        if (request.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == request.CategoryId);

        if (request.SupplierId.HasValue)
            query = query.Where(x => x.SupplierId == request.SupplierId);

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.ProductName.ToLower().Contains(term) || 
                x.Barcode.ToLower().Contains(term));
        }

        // Apply sorting
        // Apply sorting
        var sortOrder = request.GetSortOrder();
        query = request.SortBy?.ToLower() switch
        {
            "name" or "productname" => sortOrder == "desc" 
                ? query.OrderByDescending(x => x.ProductName) 
                : query.OrderBy(x => x.ProductName),
            "price" => sortOrder == "desc" 
                ? query.OrderByDescending(x => x.Price) 
                : query.OrderBy(x => x.Price),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        // Project to DTO
        var dtoQuery = query.Select(x => new ProductDto
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
        });

        var response = await PaginatedResponse<ProductDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<ProductDto>>.Success(response);
    }
}
