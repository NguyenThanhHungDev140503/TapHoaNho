using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity refresh token để quản lý JWT refresh tokens
/// </summary>
public class UserRefreshToken : BaseEntity<Guid>
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public bool IsRevoked { get; set; }

    [Required]
    public int UserId { get; set; }

    // Navigation property
    public virtual UserEntity? User { get; set; }
}
