using Application.Common.Models;

namespace Application.Features.Promotions.Queries;

public class GetPromotionsQuery : PaginationRequest<PaginatedResponse<Dtos.PromotionDto>>
{
}
