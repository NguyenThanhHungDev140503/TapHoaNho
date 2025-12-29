using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Categories.Commands;
using Application.Features.Categories.Dtos;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Categories.Handlers;

/// <summary>
/// Handler cho CreateCategoryCommand
/// </summary>
public class CreateCategoryCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<ApiResponse<CategoryDto>> Handle(
        CreateCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        var category = new CategoryEntity
        {
            CategoryName = request.CategoryName
        };
        
        await unitOfWork.Repository<CategoryEntity>().AddAsync(category);
        await unitOfWork.SaveChangesAsync();
        
        var dto = new CategoryDto
        {
            Id = category.Id,
            CategoryName = category.CategoryName,
            ProductCount = 0,
            CreatedAt = category.CreatedAt
        };
        
        return ApiResponse<CategoryDto>.Success(dto, "Tạo danh mục thành công");
    }
}
