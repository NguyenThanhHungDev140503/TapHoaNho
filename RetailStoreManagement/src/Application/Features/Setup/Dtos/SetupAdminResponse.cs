namespace Application.Features.Setup.Dtos;

/// <summary>
/// Response cho SetupAdmin. Không chứa token vì authentication
/// đã chuyển sang IdentityServer — client phải redirect đến IDS login sau setup.
/// </summary>
public class SetupAdminResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
}
