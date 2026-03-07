using Application.Abstractions.Messaging;
using Application.Features.Categories.Dtos;

namespace Application.Features.Categories.Commands;

public record UpdateCategoryCommand(int Id, string CategoryName) : ICommand<bool>;
