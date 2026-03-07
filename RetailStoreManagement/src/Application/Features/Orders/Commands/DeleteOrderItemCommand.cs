using Application.Abstractions.Messaging;

namespace Application.Features.Orders.Commands;

public record DeleteOrderItemCommand(int OrderId, int ItemId) : ICommand<bool>;
