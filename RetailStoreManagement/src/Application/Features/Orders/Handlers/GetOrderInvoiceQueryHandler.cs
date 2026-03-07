using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Orders.Dtos;
using Application.Features.Orders.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class GetOrderInvoiceQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetOrderInvoiceQuery, InvoiceDto>
{
    public async Task<ApiResponse<InvoiceDto>> Handle(
        GetOrderInvoiceQuery request, 
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Include(x => x.Customer)
            .Include(x => x.User)
            .Include(x => x.Promotion)
            .Include(x => x.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (order is null)
            throw new NotFoundException("Đơn hàng", request.Id);

        var invoice = new InvoiceDto
        {
            OrderId = order.Id,
            OrderDate = order.OrderDate,
            Status = order.Status.ToString(),
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.Name,
            CustomerPhone = order.Customer?.Phone,
            CustomerAddress = order.Customer?.Address,
            UserId = order.UserId,
            UserName = order.User?.Username ?? "",
            SubTotal = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            PromoCode = order.Promotion?.PromoCode,
            TotalAmount = order.TotalAmount - order.DiscountAmount,
            Items = order.OrderItems.Select(i => new InvoiceItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.ProductName ?? "",
                Barcode = i.Product?.Barcode,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                Subtotal = i.Subtotal
            }).ToList(),
            Payments = order.Payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod.ToString(),
                PaymentDate = p.PaymentDate
            }).ToList()
        };
        
        return ApiResponse<InvoiceDto>.Success(invoice);
    }
}
