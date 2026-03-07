using Application.Abstractions.Messaging;

namespace Application.Features.Promotions.Queries;

public record GetActivePromotionCountQuery() : IQuery<int>;
