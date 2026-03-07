using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Users.Dtos;
using Application.Features.Users.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Handlers;

public class GetUserByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async Task<ApiResponse<UserDto>> Handle(
        GetUserByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<UserEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new UserDto
            {
                Id = x.Id,
                Username = x.Username,
                FullName = x.FullName,
                Role = x.Role,
                CreatedAt = x.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            throw new NotFoundException("Người dùng", request.Id);

        return ApiResponse<UserDto>.Success(user);
    }
}
