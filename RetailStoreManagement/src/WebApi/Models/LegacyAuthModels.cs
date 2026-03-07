using System.Text.Json.Serialization;

namespace WebApi.Models;

public class LegacyLoginResponse
{
    public string Token { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; }
    public LegacyUserDto User { get; set; } = null!;
}

public class LegacyUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Role { get; set; }
}
