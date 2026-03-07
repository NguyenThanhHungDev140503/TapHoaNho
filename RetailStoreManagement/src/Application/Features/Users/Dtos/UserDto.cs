using Domain.Enums;

namespace Application.Features.Users.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
