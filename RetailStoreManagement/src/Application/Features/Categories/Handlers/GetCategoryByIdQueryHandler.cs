using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Handlers;

public class GetCategoryByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetCategoryByIdQuery, CategoryDto>
{
    public async Task<ApiResponse<CategoryDto>> Handle(
        GetCategoryByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var category = await unitOfWork.Repository<CategoryEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                CategoryName = x.CategoryName,
                ProductCount = x.Products.Count(p => !p.DeletedAt.HasValue),
                CreatedAt = x.CreatedAt,
                IsDeleted = x.DeletedAt.HasValue
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
            throw new NotFoundException("Danh mục", request.Id);

        return ApiResponse<CategoryDto>.Success(category);
    }
}
