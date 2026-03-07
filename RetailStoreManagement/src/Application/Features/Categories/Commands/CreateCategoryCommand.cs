using Application.Abstractions.Messaging;
using Application.Features.Categories.Dtos;

namespace Application.Features.Categories.Commands;

/// <summary>
/// Command tạo danh mục mới
/// </summary>
public record CreateCategoryCommand(string CategoryName) : ICommand<CategoryDto>;
