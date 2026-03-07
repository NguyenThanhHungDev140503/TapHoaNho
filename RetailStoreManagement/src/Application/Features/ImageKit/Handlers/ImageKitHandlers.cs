using Application.Abstractions.Messaging;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.ImageKit.Commands;
using Application.Features.ImageKit.Dtos;
using Application.Features.ImageKit.Queries;

namespace Application.Features.ImageKit.Handlers;

public class ImageKitHandlers(IImageKitService imageKitService) : 
    IQueryHandler<GetImageKitAuthQuery, ImageKitAuthResponseDto>,
    ICommandHandler<DeleteImageKitFileCommand>
{
    public Task<ApiResponse<ImageKitAuthResponseDto>> Handle(GetImageKitAuthQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = imageKitService.GetAuth();
            return Task.FromResult(ApiResponse<ImageKitAuthResponseDto>.Success(result));
        }
        catch (Exception ex)
        {
             return Task.FromResult(ApiResponse<ImageKitAuthResponseDto>.Failure(ex.Message, 500));
        }
    }

    public async Task<ApiResponse<bool>> Handle(DeleteImageKitFileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await imageKitService.DeleteFileAsync(request.FileId);
            return ApiResponse<bool>.Success(true, "File deleted successfully");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Failure(ex.Message, 500);
        }
    }
}
