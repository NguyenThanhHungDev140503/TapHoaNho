using Application.Abstractions.Messaging;

namespace Application.Features.Promotions.Commands;

public record DeletePromotionCommand(int Id) : ICommand<bool>;
