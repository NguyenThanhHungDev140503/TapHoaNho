using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class DeleteOrderItemCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteOrderItemCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteOrderItemCommand request, 
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork.Repository<OrderItemEntity>()
            .GetAll()
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == request.ItemId && x.OrderId == request.OrderId, cancellationToken);

        if (item is null)
            throw new NotFoundException("Chi tiết đơn hàng", request.ItemId);

        // Update order total
        if (item.Order != null)
        {
            item.Order.TotalAmount -= item.Subtotal;
            item.Order.UpdatedAt = DateTime.UtcNow;
        }
        
        await unitOfWork.Repository<OrderItemEntity>().DeleteAsync(item);
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa chi tiết đơn hàng thành công");
    }
}
