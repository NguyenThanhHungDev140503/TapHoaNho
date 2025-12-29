using Application.Abstractions.Messaging;
using Domain.Enums;

namespace Application.Features.Users.Commands;

public record UpdateUserCommand(
    int Id,
    string? Password = null,
    string? FullName = null,
    UserRole? Role = null
) : ICommand<bool>;
