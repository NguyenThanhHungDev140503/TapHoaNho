using Application.Abstractions.Messaging;

namespace Application.Features.Products.Commands;

/// <summary>
/// Command xóa sản phẩm (soft delete)
/// </summary>
public record DeleteProductCommand(int Id) : ICommand;
