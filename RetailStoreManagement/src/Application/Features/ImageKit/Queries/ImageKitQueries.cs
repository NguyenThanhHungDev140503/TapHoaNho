using Application.Abstractions.Messaging;
using Application.Features.ImageKit.Dtos;

namespace Application.Features.ImageKit.Queries;

public record GetImageKitAuthQuery() : IQuery<ImageKitAuthResponseDto>;
