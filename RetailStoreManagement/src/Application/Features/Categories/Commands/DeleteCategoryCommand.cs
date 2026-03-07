using Application.Abstractions.Messaging;

namespace Application.Features.Categories.Commands;

public record DeleteCategoryCommand(int Id) : ICommand<bool>;
