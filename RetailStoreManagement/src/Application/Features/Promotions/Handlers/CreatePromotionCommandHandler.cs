using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Promotions.Commands;
using Application.Features.Promotions.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class CreatePromotionCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreatePromotionCommand, PromotionDto>
{
    public async Task<ApiResponse<PromotionDto>> Handle(
        CreatePromotionCommand request, 
        CancellationToken cancellationToken)
    {
        // Check code exists
        var exists = await unitOfWork.Repository<PromotionEntity>()
            .AnyAsync(x => x.PromoCode == request.PromoCode && !x.DeletedAt.HasValue);
        
        if (exists)
            throw new BadRequestException("Mã khuyến mãi đã tồn tại");

        var promo = new PromotionEntity
        {
            PromoCode = request.PromoCode,
            Description = request.Description,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MinOrderAmount = request.MinOrderAmount,
            UsageLimit = request.UsageLimit,
            UsedCount = 0,
            Status = PromotionStatus.Active
        };
        
        await unitOfWork.Repository<PromotionEntity>().AddAsync(promo);
        await unitOfWork.SaveChangesAsync();

        var dto = new PromotionDto
        {
            Id = promo.Id,
            PromoCode = promo.PromoCode,
            Description = promo.Description,
            DiscountType = promo.DiscountType,
            DiscountValue = promo.DiscountValue,
            StartDate = promo.StartDate,
            EndDate = promo.EndDate,
            MinOrderAmount = promo.MinOrderAmount,
            UsageLimit = promo.UsageLimit,
            UsedCount = promo.UsedCount,
            Status = promo.Status,
            CreatedAt = promo.CreatedAt
        };
        
        return ApiResponse<PromotionDto>.Success(dto, "Tạo khuyến mãi thành công");
    }
}
