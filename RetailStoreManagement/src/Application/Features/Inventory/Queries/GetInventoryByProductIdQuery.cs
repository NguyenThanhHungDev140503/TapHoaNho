using Application.Abstractions.Messaging;
using Application.Features.Inventory.Dtos;

namespace Application.Features.Inventory.Queries;

public record GetInventoryByProductIdQuery(int ProductId) : IQuery<InventoryDto>;
