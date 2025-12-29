using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Promotions.Dtos;
using Application.Features.Promotions.Queries;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class GetPromotionsQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetPromotionsQuery, PaginatedResponse<PromotionDto>>
{
    public async Task<ApiResponse<PaginatedResponse<PromotionDto>>> Handle(
        GetPromotionsQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<PromotionEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.PromoCode.ToLower().Contains(term) || 
                (x.Description != null && x.Description.ToLower().Contains(term)));
        }

        var dtoQuery = query.Select(x => new PromotionDto
        {
            Id = x.Id,
            PromoCode = x.PromoCode,
            Description = x.Description,
            DiscountType = x.DiscountType,
            DiscountValue = x.DiscountValue,
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            MinOrderAmount = x.MinOrderAmount,
            UsageLimit = x.UsageLimit,
            UsedCount = x.UsedCount,
            Status = x.Status,
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<PromotionDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<PromotionDto>>.Success(response);
    }
}
