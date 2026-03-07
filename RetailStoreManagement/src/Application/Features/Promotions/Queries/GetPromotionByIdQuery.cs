using Application.Abstractions.Messaging;
using Application.Features.Promotions.Dtos;

namespace Application.Features.Promotions.Queries;

public record GetPromotionByIdQuery(int Id) : IQuery<PromotionDto>;
