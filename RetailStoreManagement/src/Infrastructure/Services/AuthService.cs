using Application.Features.Auth.Dtos;
using Application.Features.Auth.Services;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

/// <summary>
/// AuthService implementation - JWT authentication
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> ValidateUserAsync(string username, string password)
    {
        var user = await _unitOfWork.Repository<UserEntity>()
            .GetAllReadOnly()
            .FirstOrDefaultAsync(x => x.Username == username && !x.DeletedAt.HasValue);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            return null;

        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };
    }

    public Task<string> GenerateAccessTokenAsync(int userId)
    {
        var user = _unitOfWork.Repository<UserEntity>().GetById<int>(userId);
        if (user is null) return Task.FromResult(string.Empty);

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("FullName", user.FullName ?? "")
        };

        var expiryMinutes = double.Parse(jwtSettings["ExpiryMinutes"] ?? "60");
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public async Task<string> GenerateRefreshTokenAsync(int userId, DateTime? expiryDate = null)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        
        DateTime expiresAt;
        if (expiryDate.HasValue)
        {
            expiresAt = expiryDate.Value;
        }
        else
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var refreshExpiryDays = int.Parse(jwtSettings["RefreshExpiryDays"] ?? "7");
            expiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays);
        }
        
        var token = new UserRefreshToken
        {
            UserId = userId,
            Token = refreshToken,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        await _unitOfWork.Repository<UserRefreshToken>().AddAsync(token);
        await _unitOfWork.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken)
    {
        var token = await _unitOfWork.Repository<UserRefreshToken>()
            .GetAllReadOnly()
            .FirstOrDefaultAsync(x => 
                x.UserId == userId && 
                x.Token == refreshToken && 
                x.ExpiresAt > DateTime.UtcNow &&
                !x.IsRevoked);

        return token is not null;
    }

    public async Task RevokeRefreshTokenAsync(int userId, string refreshToken)
    {
        var token = await _unitOfWork.Repository<UserRefreshToken>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Token == refreshToken);

        if (token is not null)
        {
            token.IsRevoked = true;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public DateTime GetAccessTokenExpiration()
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var expiryMinutes = double.Parse(jwtSettings["ExpiryMinutes"] ?? "60");
        return DateTime.UtcNow.AddMinutes(expiryMinutes);
    }

    public int? GetUserIdFromExpiredToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(token)) return null;

        var jwtToken = tokenHandler.ReadJwtToken(token);
        var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        
        if (claim != null && int.TryParse(claim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }
}
