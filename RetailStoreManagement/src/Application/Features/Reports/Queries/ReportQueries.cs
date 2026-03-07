using Application.Abstractions.Messaging;
using Application.Features.Reports.Dtos;

namespace Application.Features.Reports.Queries;

public class GetRevenueReportQuery : IQuery<List<RevenueReportDto>>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? GroupBy { get; set; } = "day"; // day, week, month
}

public class GetSalesReportQuery : IQuery<List<SalesReportDto>>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? GroupBy { get; set; } = "day";
    public int? CategoryId { get; set; }
}

public record GetTopProductsQuery(DateTime StartDate, DateTime EndDate, int Page = 1, int PageSize = 10) : IQuery<List<TopProductDto>>;

public record GetTopCustomersQuery(DateTime StartDate, DateTime EndDate, int Page = 1, int PageSize = 10) : IQuery<List<TopCustomerDto>>;

public record GetPromotionReportQuery(DateTime StartDate, DateTime EndDate, int? PromoId = null, bool IncludeOrderDetails = false) : IQuery<List<PromotionReportDto>>;

public record GetInventoryForecastQuery(int? ProductId, int? CategoryId, int LookbackMonths = 3, int LeadTimeDays = 7, double SafetyStockMultiplier = 1.5) : IQuery<List<InventoryForecastDto>>;
