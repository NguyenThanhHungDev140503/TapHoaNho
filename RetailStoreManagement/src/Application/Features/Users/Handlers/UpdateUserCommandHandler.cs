using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Users.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Handlers;

public class UpdateUserCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) 
    : ICommandHandler<UpdateUserCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateUserCommand request, 
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<UserEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (user is null)
            throw new NotFoundException("Người dùng", request.Id);

        if (!string.IsNullOrEmpty(request.Password))
            user.Password = passwordHasher.HashPassword(request.Password);
        
        if (request.FullName is not null)
            user.FullName = request.FullName;
        
        if (request.Role.HasValue)
            user.Role = request.Role.Value;
        
        user.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật người dùng thành công");
    }
}
