using Application.Abstractions.Messaging;
using Application.Features.Orders.Dtos;

namespace Application.Features.Orders.Queries;

public record GetOrderInvoiceQuery(int Id) : IQuery<InvoiceDto>;
