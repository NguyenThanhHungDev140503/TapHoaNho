using Application.Common.Models;
using Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;
using WebApi.Models;

namespace WebApi.Controllers;

/// <summary>
/// Controller xác thực người dùng
/// </summary>
[Route("api/auth")]
public class AuthController(IMediator mediator, IConfiguration configuration, IWebHostEnvironment environment) : BaseApiController(mediator)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IWebHostEnvironment _environment = environment;

    /// <summary>
    /// Đăng nhập
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        
        if (result.Succeeded && result.Data != null)
        {
            var data = result.Data;
            SetTokenCookies(data.AccessToken, data.RefreshToken, data.ExpiresAt);

            // Map to Legacy Response
            var legacyResponse = new LegacyLoginResponse
            {
                Token = data.AccessToken,
                RefreshToken = data.RefreshToken,
                User = new LegacyUserDto
                {
                    Id = data.UserId,
                    Username = data.Username,
                    FullName = data.FullName ?? string.Empty,
                    Role = data.Role == "Admin" ? 0 : 1
                }
            };
            
            return Ok(ApiResponse<LegacyLoginResponse>.Success(legacyResponse));
        }

        return StatusCode(result.ResponseCode, result);
    }

    /// <summary>
    /// Đăng xuất
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand? command)
    {
        // Try get refresh token from cookie if not provided
        if (command == null || string.IsNullOrEmpty(command.RefreshToken))
        {
            var refreshTokenFromCookie = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshTokenFromCookie))
            {
                command = new LogoutCommand(refreshTokenFromCookie);
            }
        }

        if (command == null || string.IsNullOrEmpty(command.RefreshToken))
        {
             // Just clear cookies and return success even if no token found
             ClearTokenCookies();
             return Ok(ApiResponse<bool>.Success(true));
        }

        var result = await Mediator.Send(command);
        ClearTokenCookies();
        return StatusCode(result.ResponseCode, result);
    }

    /// <summary>
    /// Làm mới token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand? command)
    {
         string refreshToken = command?.RefreshToken ?? "";
         string accessToken = command?.AccessToken ?? "";

         if (string.IsNullOrEmpty(refreshToken))
         {
             refreshToken = Request.Cookies["refreshToken"] ?? "";
         }
         if (string.IsNullOrEmpty(accessToken))
         {
             accessToken = Request.Cookies["accessToken"] ?? "";
         }

         if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
         {
             return BadRequest(ApiResponse<object>.Failure("Tokens are required"));
         }
         
         var reRequest = new RefreshTokenCommand 
         { 
             AccessToken = accessToken, 
             RefreshToken = refreshToken 
         };
         var result = await Mediator.Send(reRequest);

         if (result.Succeeded && result.Data != null)
         {
              SetTokenCookies(result.Data.AccessToken, result.Data.RefreshToken, result.Data.ExpiresAt);
              
               // Map to Legacy Response structure (LoginResponse in legacy)
                var legacyResponse = new LegacyLoginResponse
                {
                    Token = result.Data.AccessToken,
                    RefreshToken = result.Data.RefreshToken,
                    User = new LegacyUserDto
                    {
                        Id = result.Data.UserId,
                        Username = result.Data.Username,
                        FullName = result.Data.FullName ?? string.Empty,
                        Role = result.Data.Role == "Admin" ? 0 : 1
                    }
                };

              return Ok(ApiResponse<LegacyLoginResponse>.Success(legacyResponse));
         }

         return StatusCode(result.ResponseCode, result);
    }

    /// <summary>
    /// Thiết lập admin đầu tiên
    /// </summary>
    [AllowAnonymous]
    [HttpPost("setup-admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    private void SetTokenCookies(string accessToken, string refreshToken, DateTime accessTokenExpiry)
    {
        // Use expiry from token or config. 
        // Logic from Legacy: RefreshToken ~ 1 day or 7 days (Config).
        // New code has RefreshToken.ExpiresAt? No, LoginResponse doesn't seem to have RefreshTokenExpiry.
        // Assuming 7 days default.
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };

        var accessTokenCookieOptions = new CookieOptions 
        {
             HttpOnly = cookieOptions.HttpOnly,
             Secure = cookieOptions.Secure,
             SameSite = cookieOptions.SameSite,
             Path = cookieOptions.Path,
             Expires = accessTokenExpiry
        };

        Response.Cookies.Append("accessToken", accessToken, accessTokenCookieOptions);

        if (!string.IsNullOrEmpty(refreshToken))
        {
             var refreshTokenCookieOptions = new CookieOptions
             {
                 HttpOnly = cookieOptions.HttpOnly,
                 Secure = cookieOptions.Secure,
                 SameSite = cookieOptions.SameSite,
                 Path = cookieOptions.Path,
                 MaxAge = TimeSpan.FromDays(7) // Default 7 days
             };
             Response.Cookies.Append("refreshToken", refreshToken, refreshTokenCookieOptions);
        }
    }

    private void ClearTokenCookies()
    {
        Response.Cookies.Delete("accessToken");
        Response.Cookies.Delete("refreshToken");
    }
}
