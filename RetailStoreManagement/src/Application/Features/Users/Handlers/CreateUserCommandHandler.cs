using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Users.Commands;
using Application.Features.Users.Dtos;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Handlers;

public class CreateUserCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) 
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<ApiResponse<UserDto>> Handle(
        CreateUserCommand request, 
        CancellationToken cancellationToken)
    {
        // Check username exists
        var exists = await unitOfWork.Repository<UserEntity>()
            .AnyAsync(x => x.Username == request.Username && !x.DeletedAt.HasValue);
        
        if (exists)
            throw new BadRequestException("Tên đăng nhập đã tồn tại");

        var user = new UserEntity
        {
            Username = request.Username,
            Password = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
            Role = request.Role
        };
        
        await unitOfWork.Repository<UserEntity>().AddAsync(user);
        await unitOfWork.SaveChangesAsync();
        
        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
        
        return ApiResponse<UserDto>.Success(dto, "Tạo người dùng thành công");
    }
}
