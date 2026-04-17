using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Extensions;
using Domain.Entities;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services;

public class CustomProfileService : IProfileService
{
    private readonly ApplicationDbContext _dbContext;

    public CustomProfileService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user == null)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim("name", user.FullName ?? string.Empty),
            new Claim("role", user.Role.ToString())
        };

        context.IssuedClaims.AddRange(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        context.IsActive = user != null;
    }
}
