using Application.Abstractions.Messaging;
using Application.Features.Products.Dtos;

namespace Application.Features.Products.Commands;

/// <summary>
/// Command tạo sản phẩm mới
/// </summary>
public record CreateProductCommand(
    string ProductName,
    string Barcode,
    decimal Price,
    int CategoryId,
    int SupplierId,
    string? Unit = "pcs",
    string? ImageUrl = null,
    string? ImageFileId = null
) : ICommand<ProductDto>;
