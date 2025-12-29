using Application.Abstractions.Messaging;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Queries;

public record GetProductsByIdsQuery(List<int> Ids) : IQuery<List<ProductDto>>;
