using Application.Common.Models;

namespace Application.Features.Users.Queries;

public class GetUsersQuery : PaginationRequest<PaginatedResponse<Dtos.UserDto>>
{
}
