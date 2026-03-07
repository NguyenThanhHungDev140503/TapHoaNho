using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Auth.Commands;
using Application.Features.Auth.Dtos;
using Application.Features.Auth.Services;

namespace Application.Features.Auth.Handlers;

/// <summary>
/// Handler cho LoginCommand
/// </summary>
public class LoginCommandHandler(IAuthService authService) 
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<ApiResponse<LoginResponse>> Handle(
        LoginCommand request, 
        CancellationToken cancellationToken)
    {
        var userInfo = await authService.ValidateUserAsync(request.Username, request.Password);
        
        if (userInfo is null)
            throw new BadRequestException("Tên đăng nhập hoặc mật khẩu không chính xác");

        // Generate tokens
        userInfo.AccessToken = await authService.GenerateAccessTokenAsync(userInfo.UserId);
        userInfo.RefreshToken = await authService.GenerateRefreshTokenAsync(userInfo.UserId);
        userInfo.ExpiresAt = authService.GetAccessTokenExpiration();
        
        return ApiResponse<LoginResponse>.Success(userInfo, "Đăng nhập thành công");
    }
}
