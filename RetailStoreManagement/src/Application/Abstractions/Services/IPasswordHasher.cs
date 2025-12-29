namespace Application.Abstractions.Services;

/// <summary>
/// Interface cho password hashing
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}
