namespace Application.Features.ImageKit.Dtos;

public class ImageKitAuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public long Expire { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
