namespace Application.Features.Reports.Dtos;

public class RevenueReportDto
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}

public class SalesReportDto
{
    public DateTime Date { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CanceledOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalSales { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class TopCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
}

public class PromotionReportDto
{
    public int PromotionId { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public decimal TotalDiscount { get; set; }
    public int OrdersCount { get; set; }
}

public class InventoryForecastDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public decimal AverageDailySales { get; set; }
    public int EstimatedDaysRemaining { get; set; }
    public bool NeedsRestock { get; set; }
}
