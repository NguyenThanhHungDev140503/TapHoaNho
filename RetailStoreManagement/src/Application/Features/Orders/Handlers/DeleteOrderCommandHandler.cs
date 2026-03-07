using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class DeleteOrderCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteOrderCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteOrderCommand request, 
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.Repository<OrderEntity>()
            .GetAll()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Inventory)
            .Include(o => o.Promotion)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (order is null)
            throw new NotFoundException("Đơn hàng", request.Id);

        // If order is paid, restore inventory before deleting
        if (order.Status == Domain.Enums.OrderStatus.Paid)
        {
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Product?.Inventory != null)
                {
                    orderItem.Product.Inventory.Quantity += orderItem.Quantity;
                    orderItem.Product.Inventory.UpdatedAt = DateTime.UtcNow;
                    // Intentionally removed manual Update call as entity is tracked
                }
            }
        }

        // If order has promotion and discount was applied, decrement used_count
        if (order.Promotion != null && order.DiscountAmount > 0)
        {
            order.Promotion.UsedCount = Math.Max(0, order.Promotion.UsedCount - 1);
            // Intentionally removed manual Update call as entity is tracked
        }

        order.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa đơn hàng thành công");
    }
}
