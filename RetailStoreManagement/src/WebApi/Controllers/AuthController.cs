using Application.Common.Models;
using Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers;

/// <summary>
/// Controller xác thực người dùng (LEGACY).
/// </summary>
/// <remarks>
/// DEPRECATED: Authentication đã chuyển sang Duende IdentityServer (https://localhost:5001).
///
/// Frontend dùng Authorization Code + PKCE + DPoP redirect đến IdentityServer
/// /connect/authorize thay vì POST credentials đến endpoints này.
///
/// - login/logout/refresh: trả 410 Gone, redirect tester đến IdentityServer
/// - setup-admin: GIỮ LẠI cho first-time bootstrap (tạo user admin đầu tiên
///   trước khi có ai login được qua IdentityServer). Sẽ xóa Phase 4 sau khi
///   có script seed riêng.
/// </remarks>
[Obsolete("Authentication đã chuyển sang IdentityServer (localhost:5001). login/logout/refresh sẽ 410. Sẽ xóa hoàn toàn ở Phase 4.")]
[Route("api/auth")]
public class AuthController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// [GONE] Đăng nhập đã chuyển sang IdentityServer.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login() => StatusCode(
        StatusCodes.Status410Gone,
        ApiResponse<object>.Failure(
            "Endpoint này đã bị loại bỏ. Sử dụng OIDC Authorization Code + PKCE + DPoP " +
            "qua IdentityServer tại https://localhost:5001/connect/authorize."
        )
    );

    /// <summary>
    /// [GONE] Đăng xuất đã chuyển sang IdentityServer.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout() => StatusCode(
        StatusCodes.Status410Gone,
        ApiResponse<object>.Failure(
            "Endpoint này đã bị loại bỏ. Sử dụng IdentityServer end_session endpoint."
        )
    );

    /// <summary>
    /// [GONE] Refresh token đã chuyển sang IdentityServer.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public IActionResult RefreshToken() => StatusCode(
        StatusCodes.Status410Gone,
        ApiResponse<object>.Failure(
            "Endpoint này đã bị loại bỏ. Sử dụng IdentityServer /connect/token với refresh_token grant + DPoP proof."
        )
    );

    /// <summary>
    /// Bootstrap: tạo admin đầu tiên (chạy 1 lần khi DB rỗng).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("setup-admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
