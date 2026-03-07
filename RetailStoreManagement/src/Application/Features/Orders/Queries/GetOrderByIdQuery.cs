using Application.Abstractions.Messaging;
using Application.Features.Orders.Dtos;

namespace Application.Features.Orders.Queries;

public record GetOrderByIdQuery(int Id) : IQuery<OrderDto>;
