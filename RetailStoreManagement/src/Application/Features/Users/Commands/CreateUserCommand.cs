using Application.Abstractions.Messaging;
using Application.Features.Users.Dtos;
using Domain.Enums;

namespace Application.Features.Users.Commands;

public record CreateUserCommand(
    string Username,
    string Password,
    string? FullName,
    UserRole Role = UserRole.Staff
) : ICommand<UserDto>;
