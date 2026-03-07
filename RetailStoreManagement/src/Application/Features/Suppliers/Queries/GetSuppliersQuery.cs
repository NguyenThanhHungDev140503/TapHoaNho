using Application.Common.Models;

namespace Application.Features.Suppliers.Queries;

public class GetSuppliersQuery : PaginationRequest<PaginatedResponse<Dtos.SupplierDto>>
{
}
