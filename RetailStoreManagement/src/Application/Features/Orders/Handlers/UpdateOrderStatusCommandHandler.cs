using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Domain.Enums;

namespace Application.Features.Orders.Handlers;

public class UpdateOrderStatusCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateOrderStatusCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateOrderStatusCommand request, 
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.Repository<OrderEntity>()
            .GetAll()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Inventory)
            .Include(o => o.Promotion)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (order is null)
            throw new NotFoundException("Đơn hàng", request.Id);

        // Nếu status không thay đổi, không cần làm gì
        if (order.Status == request.Status)
        {
            return ApiResponse<bool>.Success(true, "Trạng thái đơn hàng không thay đổi");
        }

        var oldStatus = order.Status;
        var newStatus = request.Status;

        // Handle status transitions
        if (oldStatus == OrderStatus.Pending && newStatus == OrderStatus.Paid)
        {
            // Pending → Paid: Decrease inventory and create payment
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Product.Inventory != null)
                {
                    // Check if sufficient stock before decreasing
                    if (orderItem.Product.Inventory.Quantity < orderItem.Quantity)
                    {
                        return ApiResponse<bool>.Failure($"Không đủ tồn kho cho sản phẩm {orderItem.Product.ProductName}", 400);
                    }
                    orderItem.Product.Inventory.Quantity -= orderItem.Quantity;
                    // Intentionally removed manual Update call as entity is tracked
                }
            }

            // Create payment record
            var payment = new PaymentEntity
            {
                OrderId = order.Id,
                Amount = order.TotalAmount - order.DiscountAmount,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = DateTime.UtcNow
            };
            await unitOfWork.Repository<PaymentEntity>().AddAsync(payment);
        }
        else if (oldStatus == OrderStatus.Paid && newStatus == OrderStatus.Pending)
        {
            // Paid → Pending: Rollback inventory, delete payment, and decrement promotion
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Product.Inventory != null)
                {
                    // Restore inventory
                    orderItem.Product.Inventory.Quantity += orderItem.Quantity;
                    // Intentionally removed manual Update call as entity is tracked
                }
            }

            // Delete payment record
            foreach (var payment in order.Payments)
            {
                await unitOfWork.Repository<PaymentEntity>().DeleteAsync(payment);
            }

            // Decrement promotion usedCount if was applied
            if (order.Promotion != null && order.DiscountAmount > 0)
            {
                order.Promotion.UsedCount = Math.Max(0, order.Promotion.UsedCount - 1);
                // Intentionally removed manual Update call as entity is tracked
            }
        }
        else if (oldStatus == OrderStatus.Pending && newStatus == OrderStatus.Canceled)
        {
            // Pending → Canceled: Decrement promotion usedCount if was applied
            if (order.Promotion != null && order.DiscountAmount > 0)
            {
                order.Promotion.UsedCount = Math.Max(0, order.Promotion.UsedCount - 1);
                // Intentionally removed manual Update call as entity is tracked
            }
        }
        else if (oldStatus == OrderStatus.Paid && newStatus == OrderStatus.Canceled)
        {
            // Paid → Canceled: Rollback inventory, delete payment, and decrement promotion
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Product.Inventory != null)
                {
                    // Restore inventory
                    orderItem.Product.Inventory.Quantity += orderItem.Quantity;
                    // Intentionally removed manual Update call as entity is tracked
                }
            }

            // Delete payment record
            foreach (var payment in order.Payments)
            {
                await unitOfWork.Repository<PaymentEntity>().DeleteAsync(payment);
            }

            // Decrement promotion usedCount if was applied
            if (order.Promotion != null && order.DiscountAmount > 0)
            {
                order.Promotion.UsedCount = Math.Max(0, order.Promotion.UsedCount - 1);
                // Intentionally removed manual Update call as entity is tracked
            }
        }
        else if (oldStatus == OrderStatus.Canceled && newStatus == OrderStatus.Paid)
        {
            // Canceled → Paid: Decrease inventory and create payment
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Product.Inventory != null)
                {
                    // Check if sufficient stock before decreasing
                    if (orderItem.Product.Inventory.Quantity < orderItem.Quantity)
                    {
                        return ApiResponse<bool>.Failure($"Không đủ tồn kho cho sản phẩm {orderItem.Product.ProductName}", 400);
                    }
                    orderItem.Product.Inventory.Quantity -= orderItem.Quantity;
                    // Intentionally removed manual Update call as entity is tracked
                }
            }

            // Create payment record
            var payment = new PaymentEntity
            {
                OrderId = order.Id,
                Amount = order.TotalAmount - order.DiscountAmount,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = DateTime.UtcNow
            };
            await unitOfWork.Repository<PaymentEntity>().AddAsync(payment);

            // Increment promotion usedCount if was applied
            if (order.Promotion != null && order.DiscountAmount > 0)
            {
                order.Promotion.UsedCount++;
                // Intentionally removed manual Update call as entity is tracked
            }
        }
        else if (oldStatus == OrderStatus.Canceled && newStatus == OrderStatus.Pending)
        {
            // Canceled → Pending: Increment promotion usedCount if was applied
            if (order.Promotion != null && order.DiscountAmount > 0)
            {
                order.Promotion.UsedCount++;
                // Intentionally removed manual Update call as entity is tracked
            }
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật trạng thái đơn hàng thành công");
    }
}
