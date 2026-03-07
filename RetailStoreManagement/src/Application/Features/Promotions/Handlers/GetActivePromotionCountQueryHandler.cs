using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Promotions.Queries;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Promotions.Handlers;

public class GetActivePromotionCountQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetActivePromotionCountQuery, int>
{
    public async Task<ApiResponse<int>> Handle(
        GetActivePromotionCountQuery request, 
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var count = await unitOfWork.Repository<PromotionEntity>()
            .GetAllReadOnly()
            .Where(x => x.Status == PromotionStatus.Active 
                && x.StartDate <= today 
                && x.EndDate >= today
                && !x.DeletedAt.HasValue)
            .CountAsync(cancellationToken);
            
        return ApiResponse<int>.Success(count);
    }
}
