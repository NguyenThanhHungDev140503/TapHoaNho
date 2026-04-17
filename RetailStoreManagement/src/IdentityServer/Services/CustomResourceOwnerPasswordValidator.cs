using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Domain.Entities;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace IdentityServer.Services;

public class CustomResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly ApplicationDbContext _dbContext;

    public CustomResourceOwnerPasswordValidator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == context.UserName);

        if (user == null)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "Invalid username or password"
            );
            return;
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(context.Password, user.Password))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "Invalid username or password"
            );
            return;
        }

        // Return the subject (user ID) as the identifier
        context.Result = new GrantValidationResult(
            subject: user.Id.ToString(),
            authenticationMethod: "password",
            claims: new[]
            {
                new System.Security.Claims.Claim("username", user.Username),
                new System.Security.Claims.Claim("name", user.FullName ?? string.Empty),
                new System.Security.Claims.Claim("role", user.Role.ToString())
            }
        );
    }
}
