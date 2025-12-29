using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Promotions.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class DeletePromotionCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeletePromotionCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeletePromotionCommand request, 
        CancellationToken cancellationToken)
    {
        var promo = await unitOfWork.Repository<PromotionEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (promo is null)
            throw new NotFoundException("Khuyến mãi", request.Id);

        promo.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa khuyến mãi thành công");
    }
}
