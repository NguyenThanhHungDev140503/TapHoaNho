using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Inventory.Dtos;
using Application.Features.Inventory.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Handlers;

public class GetLowStockQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetLowStockQuery, List<InventoryDto>>
{
    private const int LowStockThreshold = 10;
    
    public async Task<ApiResponse<List<InventoryDto>>> Handle(
        GetLowStockQuery request, 
        CancellationToken cancellationToken)
    {
        var lowStock = await unitOfWork.Repository<InventoryEntity>()
            .GetAllReadOnly()
            .Where(x => x.Quantity <= LowStockThreshold && !x.Product.DeletedAt.HasValue)
            .Select(x => new InventoryDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.ProductName,
                ProductBarcode = x.Product.Barcode,
                Quantity = x.Quantity,
                LastUpdated = x.UpdatedAt
            })
            .OrderBy(x => x.Quantity)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return ApiResponse<List<InventoryDto>>.Success(lowStock);
    }
}
