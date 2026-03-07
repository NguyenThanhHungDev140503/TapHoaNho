using Application.Features.Auth.Dtos;

namespace Application.Features.Auth.Services;

/// <summary>
/// Interface cho Auth Service - sẽ được implement ở Infrastructure layer
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Xác thực user và trả về thông tin cơ bản
    /// </summary>
    Task<LoginResponse?> ValidateUserAsync(string username, string password);
    
    /// <summary>
    /// Tạo access token
    /// </summary>
    Task<string> GenerateAccessTokenAsync(int userId);
    
    /// <summary>
    /// Tạo refresh token. Nếu expiryDate được cung cấp, token sẽ hết hạn vào thời điểm đó.
    /// </summary>
    Task<string> GenerateRefreshTokenAsync(int userId, DateTime? expiryDate = null);
    
    /// <summary>
    /// Kiểm tra refresh token có hợp lệ không
    /// </summary>
    Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken);
    
    /// <summary>
    /// Thu hồi refresh token
    /// </summary>
    Task RevokeRefreshTokenAsync(int userId, string refreshToken);
    
    /// <summary>
    /// Lấy thời gian hết hạn của access token
    /// </summary>
    DateTime GetAccessTokenExpiration();

    /// <summary>
    /// Lấy UserId từ Access Token (có thể đã hết hạn)
    /// </summary>
    int? GetUserIdFromExpiredToken(string token);
}
