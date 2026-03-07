using Application.Abstractions.Messaging;

namespace Application.Features.Users.Commands;

public record DeleteUserCommand(int Id) : ICommand<bool>;
