using Application.Abstractions.Messaging;
using Domain.Enums;

namespace Application.Features.Orders.Queries;

public class GetTotalRevenueQuery : IQuery<decimal>
{
    public OrderStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
