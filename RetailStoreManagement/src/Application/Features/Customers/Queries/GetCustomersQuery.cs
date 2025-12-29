using Application.Common.Models;

namespace Application.Features.Customers.Queries;

public class GetCustomersQuery : PaginationRequest<PaginatedResponse<Dtos.CustomerDto>>
{
}
