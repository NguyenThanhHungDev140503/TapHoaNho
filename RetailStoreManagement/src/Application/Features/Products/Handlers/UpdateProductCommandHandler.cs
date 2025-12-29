using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Products.Commands;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Products.Handlers;

/// <summary>
/// Handler cho UpdateProductCommand - cập nhật sản phẩm
/// </summary>
public class UpdateProductCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateProductCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateProductCommand request, 
        CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Repository<ProductEntity>().GetAsync(request.Id);
        
        if (product is null || product.DeletedAt.HasValue)
            throw new NotFoundException("Sản phẩm", request.Id);

        product.ProductName = request.ProductName;
        product.Barcode = request.Barcode;
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;
        product.SupplierId = request.SupplierId;
        product.Unit = request.Unit ?? product.Unit;
        product.ImageUrl = request.ImageUrl ?? product.ImageUrl;
        product.ImageFileId = request.ImageFileId ?? product.ImageFileId;
        product.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật sản phẩm thành công");
    }
}
