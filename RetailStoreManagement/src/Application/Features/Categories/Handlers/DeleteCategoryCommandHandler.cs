using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Categories.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Handlers;

public class DeleteCategoryCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteCategoryCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        var category = await unitOfWork.Repository<CategoryEntity>()
            .GetAll()
            .Include(c => c.Products)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (category is null)
            throw new NotFoundException("Danh mục", request.Id);

        if (category.Products.Any(p => !p.DeletedAt.HasValue))
        {
            return ApiResponse<bool>.Failure(
                $"Không thể xóa danh mục này vì đang có sản phẩm liên quan. " +
                "Vui lòng xóa hoặc chuyển các sản phẩm trước khi xóa danh mục.",
                400
            );
        }

        // Soft delete
        category.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa danh mục thành công");
    }
}
