using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Handlers;

/// <summary>
/// Handler cho GetCategoriesQuery
/// </summary>
public class GetCategoriesQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetCategoriesQuery, PaginatedResponse<CategoryDto>>
{
    public async Task<ApiResponse<PaginatedResponse<CategoryDto>>> Handle(
        GetCategoriesQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<CategoryEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => x.CategoryName.ToLower().Contains(term));
        }

        var dtoQuery = query.Select(x => new CategoryDto
        {
            Id = x.Id,
            CategoryName = x.CategoryName,
            ProductCount = x.Products.Count(p => !p.DeletedAt.HasValue),
            CreatedAt = x.CreatedAt,
            IsDeleted = x.DeletedAt.HasValue
        });

        var response = await PaginatedResponse<CategoryDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<CategoryDto>>.Success(response);
    }
}
