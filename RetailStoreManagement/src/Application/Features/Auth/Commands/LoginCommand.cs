using Application.Abstractions.Messaging;
using Application.Features.Auth.Dtos;

namespace Application.Features.Auth.Commands;

/// <summary>
/// Command đăng nhập
/// </summary>
public record LoginCommand(string Username, string Password) : ICommand<LoginResponse>;
