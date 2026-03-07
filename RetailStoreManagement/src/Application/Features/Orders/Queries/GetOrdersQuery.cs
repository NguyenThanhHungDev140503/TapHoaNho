using Application.Common.Models;
using Domain.Enums;

namespace Application.Features.Orders.Queries;

public class GetOrdersQuery : PaginationRequest<PaginatedResponse<Dtos.OrderDto>>
{
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
