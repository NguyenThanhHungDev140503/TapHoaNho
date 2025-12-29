using Application.Abstractions.Messaging;
using Application.Features.Orders.Dtos;

namespace Application.Features.Orders.Commands;

public record AddOrderItemCommand(int OrderId, int ProductId, int Quantity, decimal Price) : ICommand<OrderItemDto>;
