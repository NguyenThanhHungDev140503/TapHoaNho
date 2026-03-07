using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Products.Commands;
using Application.Features.Products.Dtos;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Products.Handlers;

/// <summary>
/// Handler cho CreateProductCommand - tạo sản phẩm mới
/// </summary>
public class CreateProductCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreateProductCommand, ProductDto>
{
    public async Task<ApiResponse<ProductDto>> Handle(
        CreateProductCommand request, 
        CancellationToken cancellationToken)
    {
        var product = new ProductEntity
        {
            ProductName = request.ProductName,
            Barcode = request.Barcode,
            Price = request.Price,
            CategoryId = request.CategoryId,
            SupplierId = request.SupplierId,
            Unit = request.Unit ?? "pcs",
            ImageUrl = request.ImageUrl,
            ImageFileId = request.ImageFileId
        };
        
        await unitOfWork.Repository<ProductEntity>().AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        // Create initial inventory
        var inventory = new InventoryEntity
        {
            ProductId = product.Id,
            Quantity = 0,
            UpdatedAt = DateTime.UtcNow
        };
        await unitOfWork.Repository<InventoryEntity>().AddAsync(inventory);
        await unitOfWork.SaveChangesAsync();
        
        var dto = new ProductDto
        {
            Id = product.Id,
            ProductName = product.ProductName,
            Barcode = product.Barcode,
            Price = product.Price,
            Unit = product.Unit,
            ImageUrl = product.ImageUrl,
            CategoryId = product.CategoryId,
            SupplierId = product.SupplierId,
            CreatedAt = product.CreatedAt
        };
        
        return ApiResponse<ProductDto>.Success(dto, "Tạo sản phẩm thành công");
    }
}
