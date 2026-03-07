using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Auth.Commands;
using Application.Features.Auth.Dtos;
using Application.Features.Auth.Services;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Handlers;

/// <summary>
/// Handler cho RefreshTokenCommand
/// </summary>
public class RefreshTokenCommandHandler(
    IAuthService authService, 
    IUnitOfWork unitOfWork) 
    : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<ApiResponse<LoginResponse>> Handle(
        RefreshTokenCommand request, 
        CancellationToken cancellationToken)
    {
        // Extract UserId from expired access token
        var userId = authService.GetUserIdFromExpiredToken(request.AccessToken);
        
        if (!userId.HasValue)
            throw new BadRequestException("Access token không hợp lệ");

        // Fetch refresh token entity to get expiration date
        var refreshTokenEntity = await unitOfWork.Repository<UserRefreshToken>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (refreshTokenEntity == null || refreshTokenEntity.IsRevoked || refreshTokenEntity.ExpiresAt <= DateTime.UtcNow)
            throw new BadRequestException("Refresh token không hợp lệ hoặc đã hết hạn");

        if (refreshTokenEntity.UserId != userId.Value)
            throw new BadRequestException("Refresh token không khớp với user");

        // Get user info
        var user = await unitOfWork.Repository<UserEntity>()
            .GetAll()
            .Where(x => x.Id == userId.Value && !x.DeletedAt.HasValue)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (user is null)
            throw new NotFoundException("Người dùng", userId.Value);

        // Capture old expiration
        var oldExpiry = refreshTokenEntity.ExpiresAt;

        // Revoke old token
        await authService.RevokeRefreshTokenAsync(userId.Value, request.RefreshToken);
        
        // Generate new tokens
        var response = new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            AccessToken = await authService.GenerateAccessTokenAsync(user.Id),
            RefreshToken = await authService.GenerateRefreshTokenAsync(user.Id, oldExpiry), // Keep original expiration
            ExpiresAt = authService.GetAccessTokenExpiration()
        };
        
        return ApiResponse<LoginResponse>.Success(response, "Làm mới token thành công");
    }
}
