using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Setup.Commands;
using Application.Features.Setup.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;

namespace Application.Features.Setup.Handlers;

public class SetupAdminCommandHandler(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher)
    : ICommandHandler<SetupAdminCommand, SetupAdminResponse>
{
    public async Task<ApiResponse<SetupAdminResponse>> Handle(
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

        var response = new SetupAdminResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };

        return ApiResponse<SetupAdminResponse>.Success(
            response,
            "Tạo admin thành công. Vui lòng đăng nhập qua IdentityServer."
        );
    }
}
