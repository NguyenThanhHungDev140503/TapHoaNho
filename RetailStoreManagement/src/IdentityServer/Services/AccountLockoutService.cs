using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services;

public class AccountLockoutService(ApplicationDbContext dbContext)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedOutAsync(int userId)
    {
        var lockedUntil = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.LockedUntil)
            .FirstOrDefaultAsync();

        return lockedUntil is DateTimeOffset until && until > DateTimeOffset.UtcNow;
    }

    public async Task<(bool isLockedOut, DateTimeOffset? lockedUntil)> RecordFailedAttemptAsync(int userId)
    {
        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) return (false, null);

        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            user.FailedLoginAttempts = 0;
            await dbContext.SaveChangesAsync();
            return (true, user.LockedUntil);
        }

        await dbContext.SaveChangesAsync();
        return (false, null);
    }

    public async Task ResetFailedAttemptsAsync(int userId)
    {
        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) return;

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await dbContext.SaveChangesAsync();
    }
}
