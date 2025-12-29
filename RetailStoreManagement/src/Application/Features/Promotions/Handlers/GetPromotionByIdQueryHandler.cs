using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Promotions.Dtos;
using Application.Features.Promotions.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class GetPromotionByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetPromotionByIdQuery, PromotionDto>
{
    public async Task<ApiResponse<PromotionDto>> Handle(
        GetPromotionByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var promo = await unitOfWork.Repository<PromotionEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new PromotionDto
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
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (promo is null)
            throw new NotFoundException("Khuyến mãi", request.Id);

        return ApiResponse<PromotionDto>.Success(promo);
    }
}
