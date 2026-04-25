using FluentAssertions;
using Infrastructure.Database;
using IdentityServer.Services;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;

namespace Tests.Unit.IdentityServer;

public class AccountLockoutServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly AccountLockoutService _sut;

    public AccountLockoutServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _sut = new AccountLockoutService(_db);
    }

    public void Dispose() => _db.Dispose();

    private UserEntity CreateActiveUser(int id = 1, int failedAttempts = 0, DateTimeOffset? lockedUntil = null)
    {
        var user = new UserEntity
        {
            Id = id,
            Username = $"user{id}",
            Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FullName = $"Test User {id}",
            Role = UserRole.Staff,
            FailedLoginAttempts = failedAttempts,
            LockedUntil = lockedUntil
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task IsLockedOut_NeverLocked_ReturnsFalse()
    {
        CreateActiveUser();

        var result = await _sut.IsLockedOutAsync(1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsLockedOut_LockedUntilInFuture_ReturnsTrue()
    {
        CreateActiveUser(lockedUntil: DateTimeOffset.UtcNow.AddMinutes(10));

        var result = await _sut.IsLockedOutAsync(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLockedOut_LockedUntilInPast_ReturnsFalse()
    {
        CreateActiveUser(lockedUntil: DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await _sut.IsLockedOutAsync(1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsLockedOut_NonExistentUser_ReturnsFalse()
    {
        var result = await _sut.IsLockedOutAsync(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecordFailedAttempt_IncrementsFailedLoginAttempts()
    {
        CreateActiveUser();

        var (isLockedOut, _) = await _sut.RecordFailedAttemptAsync(1);

        isLockedOut.Should().BeFalse();
        var user = await _db.Users.FindAsync(1);
        user!.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailedAttempt_ReachesMaxLocksOutAccount()
    {
        CreateActiveUser(failedAttempts: 4);

        var (isLockedOut, lockedUntil) = await _sut.RecordFailedAttemptAsync(1);

        isLockedOut.Should().BeTrue();
        lockedUntil.Should().NotBeNull();
        lockedUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));

        var user = await _db.Users.FindAsync(1);
        user!.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttempt_AlreadyLocked_ExtendsLockout()
    {
        CreateActiveUser(failedAttempts: 4, lockedUntil: DateTimeOffset.UtcNow.AddMinutes(10));

        var (isLockedOut, lockedUntil) = await _sut.RecordFailedAttemptAsync(1);

        isLockedOut.Should().BeTrue();
        lockedUntil.Should().NotBeNull();

        var user = await _db.Users.FindAsync(1);
        user!.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttempt_NonExistentUser_ReturnsNotLocked()
    {
        var (isLockedOut, lockedUntil) = await _sut.RecordFailedAttemptAsync(999);

        isLockedOut.Should().BeFalse();
        lockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task ResetFailedAttempts_ZerosCounterAndClearsLockedUntil()
    {
        CreateActiveUser(failedAttempts: 3, lockedUntil: DateTimeOffset.UtcNow.AddMinutes(10));

        await _sut.ResetFailedAttemptsAsync(1);

        var user = await _db.Users.FindAsync(1);
        user!.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task ResetFailedAttempts_NonExistentUser_DoesNotThrow()
    {
        var act = () => _sut.ResetFailedAttemptsAsync(999);

        await act.Should().NotThrowAsync();
    }
}
