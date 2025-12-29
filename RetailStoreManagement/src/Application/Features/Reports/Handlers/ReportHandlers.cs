using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Reports.Dtos;
using Application.Features.Reports.Queries;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Handlers;

public class GetRevenueReportQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetRevenueReportQuery, List<RevenueReportDto>>
{
    public async Task<ApiResponse<List<RevenueReportDto>>> Handle(
        GetRevenueReportQuery request, 
        CancellationToken cancellationToken)
    {
        var orders = await unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => x.OrderDate >= request.StartDate 
                && x.OrderDate <= request.EndDate 
                && x.Status == OrderStatus.Paid
                && !x.DeletedAt.HasValue)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        IEnumerable<IGrouping<DateTime, OrderEntity>> grouped = request.GroupBy?.ToLower() switch
        {
            "week" => orders.GroupBy(x => x.OrderDate.Date.AddDays(-(int)x.OrderDate.DayOfWeek)),
            "month" => orders.GroupBy(x => new DateTime(x.OrderDate.Year, x.OrderDate.Month, 1)),
            _ => orders.GroupBy(x => x.OrderDate.Date)
        };

        var result = grouped.Select(g => new RevenueReportDto
        {
            Date = g.Key,
            TotalRevenue = g.Sum(x => x.TotalAmount - x.DiscountAmount),
            OrderCount = g.Count(),
            AverageOrderValue = g.Any() ? g.Average(x => x.TotalAmount - x.DiscountAmount) : 0
        }).OrderBy(x => x.Date).ToList();
            
        return ApiResponse<List<RevenueReportDto>>.Success(result);
    }
}

public class GetSalesReportQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetSalesReportQuery, List<SalesReportDto>>
{
    public async Task<ApiResponse<List<SalesReportDto>>> Handle(
        GetSalesReportQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Include(x => x.OrderItems)
            .ThenInclude(oi => oi.Product) // Include Product for Category filtering
            .Where(x => x.OrderDate >= request.StartDate 
                && x.OrderDate <= request.EndDate 
                && !x.DeletedAt.HasValue);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.OrderItems.Any(oi => oi.Product.CategoryId == request.CategoryId));
        }

        var orders = await query
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var grouped = orders.GroupBy(x => x.OrderDate.Date);

        var result = grouped.Select(g => new SalesReportDto
        {
            Date = g.Key,
            TotalOrders = g.Count(),
            CompletedOrders = g.Count(x => x.Status == OrderStatus.Paid),
            CanceledOrders = g.Count(x => x.Status == OrderStatus.Canceled),
            PendingOrders = g.Count(x => x.Status == OrderStatus.Pending),
            // If CategoryId is filtered, calculate sales only for that category
            TotalSales = request.CategoryId.HasValue
                ? g.Where(x => x.Status == OrderStatus.Paid)
                   .SelectMany(x => x.OrderItems)
                   .Where(oi => oi.Product.CategoryId == request.CategoryId)
                   .Sum(oi => oi.Subtotal) // Assuming Subtotal is correct for item
                : g.Where(x => x.Status == OrderStatus.Paid).Sum(x => x.TotalAmount - x.DiscountAmount)
        }).OrderBy(x => x.Date).ToList();
            
        return ApiResponse<List<SalesReportDto>>.Success(result);
    }
}

public class GetTopProductsQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetTopProductsQuery, List<TopProductDto>>
{
    public async Task<ApiResponse<List<TopProductDto>>> Handle(
        GetTopProductsQuery request, 
        CancellationToken cancellationToken)
    {
        var result = await unitOfWork.Repository<OrderItemEntity>()
            .GetAllReadOnly()
            .Where(x => x.Order.OrderDate >= request.StartDate 
                && x.Order.OrderDate <= request.EndDate 
                && x.Order.Status == OrderStatus.Paid
                && !x.Order.DeletedAt.HasValue)
            .GroupBy(x => new { x.ProductId, x.Product.ProductName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Subtotal)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return ApiResponse<List<TopProductDto>>.Success(result);
    }
}

public class GetTopCustomersQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetTopCustomersQuery, List<TopCustomerDto>>
{
    public async Task<ApiResponse<List<TopCustomerDto>>> Handle(
        GetTopCustomersQuery request, 
        CancellationToken cancellationToken)
    {
        var result = await unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => x.OrderDate >= request.StartDate 
                && x.OrderDate <= request.EndDate 
                && x.Status == OrderStatus.Paid
                && x.CustomerId > 0
                && !x.DeletedAt.HasValue)
            .GroupBy(x => new { x.CustomerId, x.Customer!.Name, x.Customer.Phone })
            .Select(g => new TopCustomerDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.Name,
                Phone = g.Key.Phone,
                TotalOrders = g.Count(),
                TotalSpent = g.Sum(x => x.TotalAmount - x.DiscountAmount)
            })
            .OrderByDescending(x => x.TotalSpent)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return ApiResponse<List<TopCustomerDto>>.Success(result);
    }
}

public class GetPromotionReportQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetPromotionReportQuery, List<PromotionReportDto>>
{
    public async Task<ApiResponse<List<PromotionReportDto>>> Handle(
        GetPromotionReportQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => x.OrderDate >= request.StartDate 
                && x.OrderDate <= request.EndDate 
                && x.PromoId != null
                && !x.DeletedAt.HasValue);

        if (request.PromoId.HasValue)
        {
            query = query.Where(x => x.PromoId == request.PromoId);
        }

        var result = await query
            .GroupBy(x => new { x.PromoId, x.Promotion!.PromoCode })
            .Select(g => new PromotionReportDto
            {
                PromotionId = g.Key.PromoId ?? 0,
                PromoCode = g.Key.PromoCode,
                UsedCount = g.Count(),
                TotalDiscount = g.Sum(x => x.DiscountAmount),
                OrdersCount = g.Count()
            })
            .OrderByDescending(x => x.UsedCount)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        // IncludeOrderDetails logic can be added here (fetching orders separately if needed)
            
        return ApiResponse<List<PromotionReportDto>>.Success(result);
    }
}

public class GetInventoryForecastQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetInventoryForecastQuery, List<InventoryForecastDto>>
{
    public async Task<ApiResponse<List<InventoryForecastDto>>> Handle(
        GetInventoryForecastQuery request, 
        CancellationToken cancellationToken)
    {
        var lookbackDays = request.LookbackMonths * 30; // approx
        var startDate = DateTime.UtcNow.AddDays(-lookbackDays);
        
        // Get sales data for last N days
        var salesQuery = unitOfWork.Repository<OrderItemEntity>()
            .GetAllReadOnly()
            .Where(x => x.Order.OrderDate >= startDate 
                && x.Order.Status == OrderStatus.Paid
                && !x.Order.DeletedAt.HasValue);

        if (request.ProductId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.ProductId == request.ProductId);
        }
        if (request.CategoryId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.Product.CategoryId == request.CategoryId);
        }

        var salesData = await salesQuery
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var salesDict = salesData.ToDictionary(x => x.ProductId, x => x.TotalSold);

        // Get Inventory
        var inventoryQuery = unitOfWork.Repository<InventoryEntity>()
            .GetAllReadOnly()
            .Include(x => x.Product)
            .Where(x => !x.Product.DeletedAt.HasValue);

        if (request.ProductId.HasValue)
        {
            inventoryQuery = inventoryQuery.Where(x => x.ProductId == request.ProductId);
        }
        if (request.CategoryId.HasValue)
        {
            inventoryQuery = inventoryQuery.Where(x => x.Product.CategoryId == request.CategoryId);
        }

        var inventoryItems = await inventoryQuery
            .Select(x => new InventoryForecastDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.ProductName,
                CurrentStock = x.Quantity,
                AverageDailySales = 0, // Will be calculated
                EstimatedDaysRemaining = 0,
                NeedsRestock = false
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Calculate forecast
        foreach (var item in inventoryItems)
        {
            if (salesDict.TryGetValue(item.ProductId, out var totalSold))
            {
                // Average Daily Sales over the lookback period
                // If lookback is 30 days, we divide by 30
                // If Item was created less than 30 days ago, we might skew, but keep simple for now
                item.AverageDailySales = (decimal)totalSold / lookbackDays;
            }
            else
            {
                item.AverageDailySales = 0;
            }

            // Forecast
            if (item.AverageDailySales > 0)
            {
                 item.EstimatedDaysRemaining = (int)(item.CurrentStock / item.AverageDailySales);
                 
                 // Reorder Point = DailySales * LeadTime + SafetyStock
                 // SafetyStock = DailySales * LeadTime * (Multiplier - 1)
                 // ReorderPoint = DailySales * LeadTime * Multiplier
                 var reorderPoint = item.AverageDailySales * request.LeadTimeDays * (decimal)request.SafetyStockMultiplier;
                 
                 item.NeedsRestock = item.CurrentStock <= reorderPoint;
            }
            else
            {
                item.EstimatedDaysRemaining = 999; // Indefinite
                item.NeedsRestock = false; // No sales, no need to restock? Or maybe checking min stock level?
                // Let's assume no sales means no restock for now unless stock is 0?
                // But keeping it false is safer to avoid noise.
            }
        }
            
        return ApiResponse<List<InventoryForecastDto>>.Success(inventoryItems.OrderBy(x => x.EstimatedDaysRemaining).ToList());
    }
}
