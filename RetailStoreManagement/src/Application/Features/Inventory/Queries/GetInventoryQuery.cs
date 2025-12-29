using Application.Common.Models;

namespace Application.Features.Inventory.Queries;

public class GetInventoryQuery : PaginationRequest<PaginatedResponse<Dtos.InventoryDto>>
{
    public int? MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
}
