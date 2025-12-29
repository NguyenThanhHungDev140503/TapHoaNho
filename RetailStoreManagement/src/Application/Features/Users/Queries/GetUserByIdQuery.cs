using Application.Abstractions.Messaging;
using Application.Features.Users.Dtos;

namespace Application.Features.Users.Queries;

public record GetUserByIdQuery(int Id) : IQuery<UserDto>;
