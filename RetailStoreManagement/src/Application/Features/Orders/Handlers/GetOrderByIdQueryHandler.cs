using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Dtos;
using Application.Features.Orders.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class GetOrderByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<ApiResponse<OrderDto>> Handle(
        GetOrderByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new OrderDto
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                UserId = x.UserId,
                UserName = x.User.Username,
                UserFullName = x.User.FullName,
                PromoId = x.PromoId,
                PromoCode = x.Promotion != null ? x.Promotion.PromoCode : null,
                OrderDate = x.OrderDate,
                StatusEnum = x.Status,
                TotalAmount = x.TotalAmount,
                DiscountAmount = x.DiscountAmount,
                Items = x.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Subtotal = i.Subtotal
                }).ToList(),
                CreatedAt = x.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
            throw new NotFoundException("Đơn hàng", request.Id);

        return ApiResponse<OrderDto>.Success(order);
    }
}
