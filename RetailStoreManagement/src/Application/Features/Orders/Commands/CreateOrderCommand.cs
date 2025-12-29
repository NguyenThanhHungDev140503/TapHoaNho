using Application.Abstractions.Messaging;
using Application.Features.Orders.Dtos;
using Domain.Enums;

namespace Application.Features.Orders.Commands;

public record CreateOrderCommand(
    int? CustomerId,
    int? PromoId,
    List<CreateOrderItemDto> Items
) : ICommand<OrderDto>;

public class CreateOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
