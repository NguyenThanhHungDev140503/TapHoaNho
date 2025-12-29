using Application.Abstractions.Messaging;

namespace Application.Features.Orders.Commands;

public record DeleteOrderCommand(int Id) : ICommand<bool>;
