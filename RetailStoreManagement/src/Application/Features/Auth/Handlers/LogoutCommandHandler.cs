using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Auth.Commands;
using Application.Features.Auth.Services;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Handlers;

/// <summary>
/// Handler cho LogoutCommand
/// </summary>
public class LogoutCommandHandler(IAuthService authService, IUnitOfWork unitOfWork) 
    : ICommandHandler<LogoutCommand>
{
    public async Task<ApiResponse<bool>> Handle(
        LogoutCommand request, 
        CancellationToken cancellationToken)
    {
        // Find token to get UserId
        var tokenEntity = await unitOfWork.Repository<UserRefreshToken>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);
            
        if (tokenEntity != null)
        {
            await authService.RevokeRefreshTokenAsync(tokenEntity.UserId, request.RefreshToken);
        }
        
        return ApiResponse<bool>.Success(true, "Đăng xuất thành công");
    }
}
