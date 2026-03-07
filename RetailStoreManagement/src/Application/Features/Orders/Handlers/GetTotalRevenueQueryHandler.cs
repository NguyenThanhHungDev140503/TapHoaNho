using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Orders.Queries;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class GetTotalRevenueQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetTotalRevenueQuery, decimal>
{
    public async Task<ApiResponse<decimal>> Handle(
        GetTotalRevenueQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status);
        
        if (request.CustomerId.HasValue)
            query = query.Where(x => x.CustomerId == request.CustomerId);
            
        if (request.UserId.HasValue)
            query = query.Where(x => x.UserId == request.UserId);
            
        if (request.StartDate.HasValue)
            query = query.Where(x => x.OrderDate >= request.StartDate);
            
        if (request.EndDate.HasValue)
            query = query.Where(x => x.OrderDate <= request.EndDate);

        // Only count paid orders for revenue
        query = query.Where(x => x.Status == OrderStatus.Paid);
        
        var totalRevenue = await query.SumAsync(x => x.TotalAmount - x.DiscountAmount, cancellationToken);
            
        return ApiResponse<decimal>.Success(totalRevenue);
    }
}
