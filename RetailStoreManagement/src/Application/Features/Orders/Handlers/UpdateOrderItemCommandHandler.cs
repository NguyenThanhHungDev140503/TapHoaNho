using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class UpdateOrderItemCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateOrderItemCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateOrderItemCommand request, 
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork.Repository<OrderItemEntity>()
            .GetAll()
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == request.ItemId && x.OrderId == request.OrderId, cancellationToken);

        if (item is null)
            throw new NotFoundException("Chi tiết đơn hàng", request.ItemId);

        // Calculate difference
        var oldSubtotal = item.Subtotal;
        var newSubtotal = item.Price * request.Quantity;
        
        item.Quantity = request.Quantity;
        item.Subtotal = newSubtotal;
        
        // Update order total
        if (item.Order != null)
        {
            item.Order.TotalAmount += (newSubtotal - oldSubtotal);
            item.Order.UpdatedAt = DateTime.UtcNow;
        }
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật chi tiết đơn hàng thành công");
    }
}
