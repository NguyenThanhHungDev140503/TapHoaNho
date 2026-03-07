using Application.Abstractions.Messaging;
using Application.Features.Inventory.Dtos;

namespace Application.Features.Inventory.Commands;

public record UpdateInventoryCommand(
    int ProductId,
    int QuantityChange,
    string Reason,
    int UserId
) : ICommand<InventoryDto>;
