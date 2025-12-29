using Application.Common.Models;

namespace Application.Features.Categories.Queries;

/// <summary>
/// Query lấy danh sách danh mục
/// </summary>
public class GetCategoriesQuery : PaginationRequest<PaginatedResponse<Dtos.CategoryDto>>
{
}
