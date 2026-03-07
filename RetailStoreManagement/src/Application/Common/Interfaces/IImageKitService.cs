using Application.Features.ImageKit.Dtos;

namespace Application.Common.Interfaces;

public interface IImageKitService
{
    ImageKitAuthResponseDto GetAuth();
    Task DeleteFileAsync(string fileId);
}
