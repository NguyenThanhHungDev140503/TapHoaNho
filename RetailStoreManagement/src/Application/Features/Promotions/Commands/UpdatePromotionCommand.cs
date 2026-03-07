using Application.Abstractions.Messaging;
using Domain.Enums;

namespace Application.Features.Promotions.Commands;

public record UpdatePromotionCommand(
    int Id,
    string? Description = null,
    DiscountType? DiscountType = null,
    decimal? DiscountValue = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? MinOrderAmount = null,
    int? UsageLimit = null,
    PromotionStatus? Status = null
) : ICommand<bool>;
