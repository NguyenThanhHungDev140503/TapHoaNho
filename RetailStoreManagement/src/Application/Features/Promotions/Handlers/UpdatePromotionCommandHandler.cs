using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Promotions.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class UpdatePromotionCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdatePromotionCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdatePromotionCommand request, 
        CancellationToken cancellationToken)
    {
        var promo = await unitOfWork.Repository<PromotionEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (promo is null)
            throw new NotFoundException("Khuyến mãi", request.Id);

        if (request.Description is not null)
            promo.Description = request.Description;
        if (request.DiscountType.HasValue)
            promo.DiscountType = request.DiscountType.Value;
        if (request.DiscountValue.HasValue)
            promo.DiscountValue = request.DiscountValue.Value;
        if (request.StartDate.HasValue)
            promo.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue)
            promo.EndDate = request.EndDate.Value;
        if (request.MinOrderAmount.HasValue)
            promo.MinOrderAmount = request.MinOrderAmount.Value;
        if (request.UsageLimit.HasValue)
            promo.UsageLimit = request.UsageLimit.Value;
        if (request.Status.HasValue)
            promo.Status = request.Status.Value;
        
        promo.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật khuyến mãi thành công");
    }
}
