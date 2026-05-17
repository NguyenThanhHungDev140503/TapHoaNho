using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using IdentityServer.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Tests.Unit.IdentityServer;

public class CustomProfileServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CustomProfileService _sut;

    public CustomProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _sut = new CustomProfileService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ─── GetProfileDataAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetProfileData_ExistingUser_EmitsUsernameNameAndRoleClaims()
    {
        var user = new UserEntity
        {
            Id = 1,
            Username = "admin",
            Password = "hash",
            FullName = "Admin User",
            Role = UserRole.Admin
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildProfileContext(subjectId: "1");
        await _sut.GetProfileDataAsync(context);

        var claimTypes = context.IssuedClaims.Select(c => c.Type).ToList();
        claimTypes.Should().Contain(JwtClaimTypes.PreferredUserName);
        claimTypes.Should().Contain(JwtClaimTypes.Name);
        claimTypes.Should().Contain(JwtClaimTypes.Role);
    }

    [Fact]
    public async Task GetProfileData_ExistingUser_EmitsCorrectValues()
    {
        var user = new UserEntity
        {
            Id = 2,
            Username = "staff01",
            Password = "hash",
            FullName = "Staff One",
            Role = UserRole.Staff
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildProfileContext(subjectId: "2");
        await _sut.GetProfileDataAsync(context);

        context.IssuedClaims.Should().Contain(c =>
            c.Type == JwtClaimTypes.PreferredUserName && c.Value == "staff01");
        context.IssuedClaims.Should().Contain(c =>
            c.Type == JwtClaimTypes.Name && c.Value == "Staff One");
        context.IssuedClaims.Should().Contain(c =>
            c.Type == JwtClaimTypes.Role && c.Value == "Staff");
    }

    [Fact]
    public async Task GetProfileData_UserWithNullFullName_UsesUsernameAsName()
    {
        var user = new UserEntity
        {
            Id = 3,
            Username = "noname",
            Password = "hash",
            FullName = null,
            Role = UserRole.Staff
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildProfileContext(subjectId: "3");
        await _sut.GetProfileDataAsync(context);

        context.IssuedClaims.Should().Contain(c =>
            c.Type == JwtClaimTypes.Name && c.Value == "noname");
    }

    [Fact]
    public async Task GetProfileData_UserNotFound_EmitsNoClaims()
    {
        var context = BuildProfileContext(subjectId: "999");
        await _sut.GetProfileDataAsync(context);

        context.IssuedClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileData_NonNumericSubjectId_EmitsNoClaims()
    {
        var context = BuildProfileContext(subjectId: "not-a-number");
        await _sut.GetProfileDataAsync(context);

        context.IssuedClaims.Should().BeEmpty();
    }

    // ─── IsActiveAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task IsActive_ExistingUser_SetsIsActiveTrue()
    {
        var user = new UserEntity
        {
            Id = 10,
            Username = "active",
            Password = "hash",
            Role = UserRole.Staff
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildIsActiveContext(subjectId: "10");
        await _sut.IsActiveAsync(context);

        context.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsActive_DeletedUser_SetsIsActiveFalse()
    {
        var user = new UserEntity
        {
            Id = 11,
            Username = "gone",
            Password = "hash",
            Role = UserRole.Staff,
            DeletedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildIsActiveContext(subjectId: "11");
        await _sut.IsActiveAsync(context);

        context.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActive_UserNotFound_SetsIsActiveFalse()
    {
        var context = BuildIsActiveContext(subjectId: "999");
        await _sut.IsActiveAsync(context);

        context.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActive_LockedUser_SetsIsActiveFalse()
    {
        var user = new UserEntity
        {
            Id = 12,
            Username = "locked",
            Password = "hash",
            Role = UserRole.Staff,
            LockedUntil = DateTimeOffset.UtcNow.AddHours(1)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var context = BuildIsActiveContext(subjectId: "12");
        await _sut.IsActiveAsync(context);

        context.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActive_NonNumericSubjectId_SetsIsActiveFalse()
    {
        var context = BuildIsActiveContext(subjectId: "not-a-number");
        await _sut.IsActiveAsync(context);

        context.IsActive.Should().BeFalse();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static ProfileDataRequestContext BuildProfileContext(string subjectId)
    {
        var principal = BuildPrincipal(subjectId);
        return new ProfileDataRequestContext(
            principal,
            new Client(),
            "test-caller",
            requestedClaimTypes: []);
    }

    private static IsActiveContext BuildIsActiveContext(string subjectId)
    {
        var principal = BuildPrincipal(subjectId);
        return new IsActiveContext(principal, new Client(), "test-caller");
    }

    private static ClaimsPrincipal BuildPrincipal(string subjectId)
    {
        var claims = new[] { new Claim(JwtClaimTypes.Subject, subjectId) };
        var identity = new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }
}
