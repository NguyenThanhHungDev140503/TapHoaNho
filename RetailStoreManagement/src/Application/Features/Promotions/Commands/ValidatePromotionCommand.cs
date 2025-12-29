using Application.Abstractions.Messaging;
using Application.Features.Promotions.Dtos;

namespace Application.Features.Promotions.Commands;

public record ValidatePromotionCommand(string PromoCode, decimal OrderAmount) : ICommand<ValidatePromotionResult>;
