using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
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

        if (!int.TryParse(userId, out var id))
            return;

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return;

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, user.Id.ToString()),
            new Claim(JwtClaimTypes.PreferredUserName, user.Username),
            new Claim(JwtClaimTypes.Name, user.FullName ?? user.Username),
            new Claim(JwtClaimTypes.Role, user.Role.ToString())
        };

        context.IssuedClaims.AddRange(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();

        if (!int.TryParse(userId, out var id))
        {
            context.IsActive = false;
            return;
        }

        // Projection to avoid loading full entity for every token request
        var isActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == id
                && u.DeletedAt == null
                && (u.LockedUntil == null || u.LockedUntil <= DateTimeOffset.UtcNow));
        context.IsActive = isActive;
    }
}
