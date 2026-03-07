using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Users.Dtos;
using Application.Features.Users.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Handlers;

public class GetUsersQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetUsersQuery, PaginatedResponse<UserDto>>
{
    public async Task<ApiResponse<PaginatedResponse<UserDto>>> Handle(
        GetUsersQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<UserEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.Username.ToLower().Contains(term) || 
                (x.FullName != null && x.FullName.ToLower().Contains(term)));
        }

        var dtoQuery = query.Select(x => new UserDto
        {
            Id = x.Id,
            Username = x.Username,
            FullName = x.FullName,
            Role = x.Role,
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<UserDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<UserDto>>.Success(response);
    }
}
