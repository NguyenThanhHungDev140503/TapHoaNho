using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Categories.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Handlers;

public class UpdateCategoryCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateCategoryCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        var category = await unitOfWork.Repository<CategoryEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (category is null)
            throw new NotFoundException("Danh mục", request.Id);

        category.CategoryName = request.CategoryName;
        category.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật danh mục thành công");
    }
}
