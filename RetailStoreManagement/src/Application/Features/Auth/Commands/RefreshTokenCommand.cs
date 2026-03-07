using Application.Features.Auth.Dtos;
using Application.Abstractions.Messaging;

namespace Application.Features.Auth.Commands;

/// <summary>
/// Command refresh token
/// </summary>
public class RefreshTokenCommand : ICommand<LoginResponse>
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
