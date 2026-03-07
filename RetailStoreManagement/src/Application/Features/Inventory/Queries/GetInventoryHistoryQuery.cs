using Application.Common.Models;
using Application.Features.Inventory.Dtos;

namespace Application.Features.Inventory.Queries;

public class GetInventoryHistoryQuery : PaginationRequest<PaginatedResponse<InventoryHistoryDto>>
{
    public int ProductId { get; set; }
}
