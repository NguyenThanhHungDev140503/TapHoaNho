using Application.Abstractions.Messaging;
using Application.Features.Setup.Dtos;

namespace Application.Features.Setup.Commands;

/// <summary>
/// Command bootstrap: tạo admin đầu tiên của hệ thống.
/// Chỉ chạy được 1 lần khi chưa có admin nào. Sau đó authentication
/// được thực hiện qua Duende IdentityServer (OIDC Authorization Code + PKCE + DPoP).
/// </summary>
public record SetupAdminCommand(
    string Username,
    string Password,
    string? FullName
) : ICommand<SetupAdminResponse>;
