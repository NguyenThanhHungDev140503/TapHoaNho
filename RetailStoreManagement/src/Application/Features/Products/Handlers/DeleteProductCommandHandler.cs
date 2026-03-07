using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Products.Commands;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Products.Handlers;

/// <summary>
/// Handler cho DeleteProductCommand - xóa sản phẩm (soft delete)
/// </summary>
public class DeleteProductCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteProductCommand>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteProductCommand request, 
        CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Repository<ProductEntity>().GetAll()
            .Include(p => p.OrderItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        
        if (product is null || product.DeletedAt.HasValue)
            throw new NotFoundException("Sản phẩm", request.Id);

        if (product.OrderItems.Count > 0)
        {
             return ApiResponse<bool>.Failure(
                $"Không thể xóa sản phẩm này vì đang có {product.OrderItems.Count} chi tiết đơn hàng liên quan. " +
                "Vui lòng xóa các đơn hàng liên quan trước khi xóa sản phẩm.",
                400
            );
        }

        // Soft delete
        product.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa sản phẩm thành công");
    }
}
