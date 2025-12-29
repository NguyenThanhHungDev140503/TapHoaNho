using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Inventory.Dtos;
using Application.Features.Inventory.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Handlers;

public class GetInventoryQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetInventoryQuery, PaginatedResponse<InventoryDto>>
{
    public async Task<ApiResponse<PaginatedResponse<InventoryDto>>> Handle(
        GetInventoryQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<InventoryEntity>()
            .GetAllReadOnly()
            .Where(x => x.Product != null && !x.Product.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.Product.ProductName.ToLower().Contains(term) || 
                (x.Product.Barcode != null && x.Product.Barcode.Contains(term)));
        }

        if (request.MinQuantity.HasValue)
            query = query.Where(x => x.Quantity >= request.MinQuantity);
        
        if (request.MaxQuantity.HasValue)
            query = query.Where(x => x.Quantity <= request.MaxQuantity);

        if (request.CategoryId.HasValue)
            query = query.Where(x => x.Product.CategoryId == request.CategoryId);

        if (request.SupplierId.HasValue)
            query = query.Where(x => x.Product.SupplierId == request.SupplierId);

        var dtoQuery = query.Select(x => new InventoryDto
        {
            Id = x.Id,
            ProductId = x.ProductId,
            ProductName = x.Product.ProductName,
            ProductBarcode = x.Product.Barcode,
            Quantity = x.Quantity,
            LastUpdated = x.UpdatedAt
        });

        var response = await PaginatedResponse<InventoryDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<InventoryDto>>.Success(response);
    }
}
