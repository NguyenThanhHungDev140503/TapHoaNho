using Application.Abstractions.Messaging;

namespace Application.Features.Orders.Commands;

public record UpdateOrderItemCommand(int OrderId, int ItemId, int Quantity) : ICommand<bool>;
