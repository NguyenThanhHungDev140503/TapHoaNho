using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Commands;
using Application.Features.Orders.Dtos;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class AddOrderItemCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<AddOrderItemCommand, OrderItemDto>
{
    public async Task<ApiResponse<OrderItemDto>> Handle(
        AddOrderItemCommand request, 
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.Repository<OrderEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && !x.DeletedAt.HasValue, cancellationToken);

        if (order is null)
            throw new NotFoundException("Đơn hàng", request.OrderId);

        var product = await unitOfWork.Repository<ProductEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.DeletedAt.HasValue, cancellationToken);

        if (product is null)
            throw new NotFoundException("Sản phẩm", request.ProductId);

        var subtotal = request.Price * request.Quantity;
        var item = new OrderItemEntity
        {
            OrderId = request.OrderId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Price = request.Price,
            Subtotal = subtotal
        };
        
        await unitOfWork.Repository<OrderItemEntity>().AddAsync(item);
        
        // Update order total
        order.TotalAmount += subtotal;
        order.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();

        var dto = new OrderItemDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = product.ProductName,
            Quantity = item.Quantity,
            Price = item.Price,
            Subtotal = item.Subtotal
        };
        
        return ApiResponse<OrderItemDto>.Success(dto, "Thêm sản phẩm vào đơn hàng thành công");
    }
}
