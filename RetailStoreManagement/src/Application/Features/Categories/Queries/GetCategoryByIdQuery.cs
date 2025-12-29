using Application.Abstractions.Messaging;
using Application.Features.Categories.Dtos;

namespace Application.Features.Categories.Queries;

public record GetCategoryByIdQuery(int Id) : IQuery<CategoryDto>;
