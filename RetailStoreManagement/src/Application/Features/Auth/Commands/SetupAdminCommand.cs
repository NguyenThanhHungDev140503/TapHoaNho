using Application.Abstractions.Messaging;
using Application.Features.Auth.Dtos;

namespace Application.Features.Auth.Commands;

public record SetupAdminCommand(
    string Username,
    string Password,
    string? FullName
) : ICommand<LoginResponse>;
