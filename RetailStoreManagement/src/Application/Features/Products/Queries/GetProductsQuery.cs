using Application.Common.Models;

namespace Application.Features.Products.Queries;

/// <summary>
/// Query lấy danh sách sản phẩm với pagination
/// </summary>
public class GetProductsQuery : PaginationRequest<PaginatedResponse<Dtos.ProductDto>>
{
    /// <summary>
    /// Lọc theo danh mục
    /// </summary>
    public int? CategoryId { get; set; }
    
    /// <summary>
    /// Lọc theo nhà cung cấp
    /// </summary>
    public int? SupplierId { get; set; }
}
