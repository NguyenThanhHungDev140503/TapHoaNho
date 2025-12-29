using Application.Abstractions.Messaging;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Queries;

/// <summary>
/// Query lấy chi tiết sản phẩm theo ID
/// </summary>
public record GetProductByIdQuery(int Id) : IQuery<ProductDto>;
