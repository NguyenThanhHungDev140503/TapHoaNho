using Application.Abstractions.Messaging;
using Application.Features.Promotions.Dtos;
using Domain.Enums;

namespace Application.Features.Promotions.Commands;

public record CreatePromotionCommand(
    string PromoCode,
    string? Description,
    DiscountType DiscountType,
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MinOrderAmount,
    int UsageLimit
) : ICommand<PromotionDto>;
