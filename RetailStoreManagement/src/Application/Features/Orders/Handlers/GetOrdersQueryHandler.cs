using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Orders.Dtos;
using Application.Features.Orders.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Handlers;

public class GetOrdersQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetOrdersQuery, PaginatedResponse<OrderDto>>
{
    public async Task<ApiResponse<PaginatedResponse<OrderDto>>> Handle(
        GetOrdersQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<OrderEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        // Apply filters
        if (request.CustomerId.HasValue)
            query = query.Where(x => x.CustomerId == request.CustomerId);
        if (request.UserId.HasValue)
            query = query.Where(x => x.UserId == request.UserId);
        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status);
        if (request.FromDate.HasValue)
            query = query.Where(x => x.OrderDate >= request.FromDate);
        if (request.ToDate.HasValue)
            query = query.Where(x => x.OrderDate <= request.ToDate);

        // Order by latest
        query = query.OrderByDescending(x => x.OrderDate);

        var dtoQuery = query.Select(x => new OrderDto
        {
            Id = x.Id,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer != null ? x.Customer.Name : null,
            CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
            UserId = x.UserId,
            UserName = x.User.Username,
            UserFullName = x.User.FullName,
            PromoId = x.PromoId,
            PromoCode = x.Promotion != null ? x.Promotion.PromoCode : null,
            OrderDate = x.OrderDate,
            StatusEnum = x.Status, // Map to Enum property
            TotalAmount = x.TotalAmount,
            DiscountAmount = x.DiscountAmount,
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<OrderDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<OrderDto>>.Success(response);
    }
}
