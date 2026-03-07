using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Inventory.Dtos;
using Application.Features.Inventory.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Handlers;

public class GetInventoryByProductIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetInventoryByProductIdQuery, InventoryDto>
{
    public async Task<ApiResponse<InventoryDto>> Handle(
        GetInventoryByProductIdQuery request, 
        CancellationToken cancellationToken)
    {
        var inventory = await unitOfWork.Repository<InventoryEntity>()
            .GetAll()
            .Where(x => x.ProductId == request.ProductId && !x.Product.DeletedAt.HasValue)
            .Select(x => new InventoryDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.ProductName,
                ProductBarcode = x.Product.Barcode,
                Quantity = x.Quantity,
                LastUpdated = x.UpdatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (inventory is null)
            throw new NotFoundException("Tồn kho sản phẩm", request.ProductId);

        return ApiResponse<InventoryDto>.Success(inventory);
    }
}
