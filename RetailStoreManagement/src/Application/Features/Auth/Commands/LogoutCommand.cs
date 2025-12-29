using Application.Abstractions.Messaging;

namespace Application.Features.Auth.Commands;

/// <summary>
/// Command đăng xuất
/// </summary>
public record LogoutCommand(string RefreshToken) : ICommand;
