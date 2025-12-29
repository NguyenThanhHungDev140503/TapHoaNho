using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Promotions.Commands;
using Application.Features.Promotions.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class ValidatePromotionCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<ValidatePromotionCommand, ValidatePromotionResult>
{
    public async Task<ApiResponse<ValidatePromotionResult>> Handle(
        ValidatePromotionCommand request, 
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var promo = await unitOfWork.Repository<PromotionEntity>()
            .GetAllReadOnly()
            .FirstOrDefaultAsync(x => 
                x.PromoCode == request.PromoCode 
                && x.Status == PromotionStatus.Active
                && x.StartDate <= today
                && x.EndDate >= today
                && !x.DeletedAt.HasValue, 
                cancellationToken);

        if (promo is null)
        {
            return ApiResponse<ValidatePromotionResult>.Success(new ValidatePromotionResult
            {
                IsValid = false,
                Message = "Mã khuyến mãi không tồn tại hoặc đã hết hạn"
            });
        }

        if (promo.UsageLimit > 0 && promo.UsedCount >= promo.UsageLimit)
        {
            return ApiResponse<ValidatePromotionResult>.Success(new ValidatePromotionResult
            {
                IsValid = false,
                Message = "Mã khuyến mãi đã hết lượt sử dụng"
            });
        }

        if (request.OrderAmount < promo.MinOrderAmount)
        {
            return ApiResponse<ValidatePromotionResult>.Success(new ValidatePromotionResult
            {
                IsValid = false,
                Message = $"Đơn hàng tối thiểu {promo.MinOrderAmount:N0}đ để áp dụng mã này"
            });
        }

        var discountAmount = promo.DiscountType == DiscountType.Percent
            ? request.OrderAmount * promo.DiscountValue / 100
            : promo.DiscountValue;

        return ApiResponse<ValidatePromotionResult>.Success(new ValidatePromotionResult
        {
            IsValid = true,
            PromoId = promo.Id,
            // PromoCode // Legacy didn't return PromoCode in DTO explicitly but maybe useful. DTO in step 1299 legacy didn't show it but step 1309 legacy response shows PromoId. My created DTO in 1324 has PromoId.
            DiscountAmount = discountAmount,
            Message = "Áp dụng mã khuyến mãi thành công"
        });
    }
}
