using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Application.Features.Orders.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class CreateOrderCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<ApiResponse<OrderDto>> Handle(
        CreateOrderCommand request, 
        CancellationToken cancellationToken)
    {
        // Validate products exist
        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await unitOfWork.Repository<ProductEntity>()
            .GetAllReadOnly()
            .Where(x => productIds.Contains(x.Id) && !x.DeletedAt.HasValue)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        
        if (products.Count != productIds.Count)
            throw new BadRequestException("Có sản phẩm không tồn tại");

        // Calculate totals
        decimal totalAmount = 0;
        var orderItems = request.Items.Select(item =>
        {
            var subtotal = item.Price * item.Quantity;
            totalAmount += subtotal;
            return new OrderItemEntity
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Price = item.Price,
                Subtotal = subtotal
            };
        }).ToList();

        // Calculate discount if promo exists
        decimal discountAmount = 0;
        PromotionEntity? promo = null;
        if (request.PromoId.HasValue)
        {
            promo = await unitOfWork.Repository<PromotionEntity>()
                .GetAll()
                .FirstOrDefaultAsync(x => x.Id == request.PromoId && x.Status == PromotionStatus.Active, cancellationToken);
            
            if (promo != null && totalAmount >= promo.MinOrderAmount)
            {
            discountAmount = promo.DiscountType == DiscountType.Percent
                    ? totalAmount * promo.DiscountValue / 100
                    : promo.DiscountValue;
            }
        }

        var order = new OrderEntity
        {
            CustomerId = request.CustomerId ?? 0,
            UserId = 1, // TODO: Get from current user context
            PromoId = request.PromoId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            OrderItems = orderItems
        };
        
        // Increment promo usage if applied
        if (promo != null && discountAmount > 0)
        {
            promo.UsedCount++;
            // Intentionally removed manual Update call as entity is tracked
        }

        await unitOfWork.Repository<OrderEntity>().AddAsync(order);
        await unitOfWork.SaveChangesAsync();

        var dto = new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            UserId = order.UserId,
            PromoId = order.PromoId,
            OrderDate = order.OrderDate,
            StatusEnum = order.Status,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            Items = orderItems.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = products[i.ProductId].ProductName,
                Quantity = i.Quantity,
                Price = i.Price,
                Subtotal = i.Subtotal
            }).ToList(),
            CreatedAt = order.CreatedAt
        };
        
        return ApiResponse<OrderDto>.Success(dto, "Tạo đơn hàng thành công");
    }
}
