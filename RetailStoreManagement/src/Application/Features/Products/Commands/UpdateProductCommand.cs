using Application.Abstractions.Messaging;

namespace Application.Features.Products.Commands;

/// <summary>
/// Command cập nhật sản phẩm
/// </summary>
public record UpdateProductCommand(
    int Id,
    string ProductName,
    string Barcode,
    decimal Price,
    int CategoryId,
    int SupplierId,
    string? Unit = "pcs",
    string? ImageUrl = null,
    string? ImageFileId = null
) : ICommand<bool>;
