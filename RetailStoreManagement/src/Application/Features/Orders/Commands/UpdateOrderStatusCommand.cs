using Application.Abstractions.Messaging;
using Domain.Enums;

namespace Application.Features.Orders.Commands;

public record UpdateOrderStatusCommand(int Id, OrderStatus Status) : ICommand<bool>;
