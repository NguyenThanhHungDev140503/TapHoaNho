using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Auth.Commands;
using Application.Features.Auth.Dtos;
using Application.Features.Auth.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Handlers;

public class SetupAdminCommandHandler(
    IUnitOfWork unitOfWork, 
    IPasswordHasher passwordHasher,
    IAuthService authService) 
    : ICommandHandler<SetupAdminCommand, LoginResponse>
{
    public async Task<ApiResponse<LoginResponse>> Handle(
        SetupAdminCommand request, 
        CancellationToken cancellationToken)
    {
        // Check if any admin exists
        var adminExists = await unitOfWork.Repository<UserEntity>()
            .AnyAsync(x => x.Role == UserRole.Admin && !x.DeletedAt.HasValue);
        
        if (adminExists)
            throw new BadRequestException("Đã tồn tại admin trong hệ thống");

        var user = new UserEntity
        {
            Username = request.Username,
            Password = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
            Role = UserRole.Admin
        };
        
        await unitOfWork.Repository<UserEntity>().AddAsync(user);
        await unitOfWork.SaveChangesAsync();

        // Generate tokens
        var accessToken = await authService.GenerateAccessTokenAsync(user.Id);
        var refreshToken = await authService.GenerateRefreshTokenAsync(user.Id);

        var response = new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = authService.GetAccessTokenExpiration()
        };

        return ApiResponse<LoginResponse>.Success(response, "Tạo admin thành công");
    }
}
