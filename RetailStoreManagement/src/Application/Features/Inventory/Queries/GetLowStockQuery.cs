using Application.Abstractions.Messaging;
using Application.Features.Inventory.Dtos;

namespace Application.Features.Inventory.Queries;

public record GetLowStockQuery() : IQuery<List<InventoryDto>>;
