using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Inventory.Commands;
using Application.Features.Inventory.Dtos;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Handlers;

public class UpdateInventoryCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateInventoryCommand, InventoryDto>
{
    public async Task<ApiResponse<InventoryDto>> Handle(
        UpdateInventoryCommand request, 
        CancellationToken cancellationToken)
    {
        var inventory = await unitOfWork.Repository<InventoryEntity>()
            .GetAll()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, cancellationToken);

        if (inventory is null)
            throw new NotFoundException("Tồn kho sản phẩm", request.ProductId);

        var newQuantity = inventory.Quantity + request.QuantityChange;
        if (newQuantity < 0)
            throw new BadRequestException("Số lượng tồn kho không thể âm");

        // Update inventory
        inventory.Quantity = newQuantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        // Create history
        var history = new InventoryHistoryEntity
        {
            ProductId = request.ProductId,
            UserId = request.UserId,
            QuantityChange = request.QuantityChange,
            QuantityAfter = newQuantity,
            Reason = request.Reason
        };
        
        await unitOfWork.Repository<InventoryHistoryEntity>().AddAsync(history);
        await unitOfWork.SaveChangesAsync();

        var dto = new InventoryDto
        {
            Id = inventory.Id,
            ProductId = inventory.ProductId,
            ProductName = inventory.Product?.ProductName ?? "",
            ProductBarcode = inventory.Product?.Barcode,
            Quantity = inventory.Quantity,
            LastUpdated = inventory.UpdatedAt
        };
        
        return ApiResponse<InventoryDto>.Success(dto, "Cập nhật tồn kho thành công");
    }
}
