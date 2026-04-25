using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace Tests.Unit.WebApi;

/// <summary>
/// Tests for the RetailApi authorization policy scope assertion.
///
/// The policy uses RequireAssertion because RFC 9068 mandates the `scope`
/// claim is a SINGLE space-separated string ("openid profile retail-api"),
/// not a JSON array. RequireClaim does exact match and would fail.
/// These tests verify the split logic is correct.
/// </summary>
public class RetailApiPolicyTests
{
    // The assertion extracted from Program.cs — mirrors the production logic exactly.
    private static readonly Func<AuthorizationHandlerContext, bool> ScopeAssertion =
        ctx => ctx.User
            .FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("retail-api");

    [Fact]
    public void Assertion_SingleScopeString_ContainingRetailApi_ReturnsTrue()
    {
        var user = BuildUser(scopeClaim: "openid profile retail-api");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeTrue();
    }

    [Fact]
    public void Assertion_OnlyRetailApiScope_ReturnsTrue()
    {
        var user = BuildUser(scopeClaim: "retail-api");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeTrue();
    }

    [Fact]
    public void Assertion_ScopeStringMissingRetailApi_ReturnsFalse()
    {
        var user = BuildUser(scopeClaim: "openid profile");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeFalse();
    }

    [Fact]
    public void Assertion_EmptyScopeClaim_ReturnsFalse()
    {
        var user = BuildUser(scopeClaim: "");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeFalse();
    }

    [Fact]
    public void Assertion_NoScopeClaim_ReturnsFalse()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "1")], authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeFalse();
    }

    [Fact]
    public void Assertion_PartialScopeNameMatch_DoesNotSatisfy()
    {
        // "retail-api-readonly" must NOT match "retail-api"
        var user = BuildUser(scopeClaim: "openid retail-api-readonly");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeFalse();
    }

    [Fact]
    public void Assertion_ExtraWhitespaceBetweenScopes_IsTolerated()
    {
        // Split with RemoveEmptyEntries handles multiple spaces
        var user = BuildUser(scopeClaim: "openid  retail-api");
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeTrue();
    }

    [Fact]
    public void Assertion_MultipleScopeClaimsOneContainingRetailApi_ReturnsTrue()
    {
        // Some servers may issue scope as multiple claims rather than one string.
        var claims = new[]
        {
            new Claim("sub", "1"),
            new Claim("scope", "openid profile"),
            new Claim("scope", "retail-api offline_access"),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var ctx = BuildContext(user);

        ScopeAssertion(ctx).Should().BeTrue();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildUser(string scopeClaim)
    {
        var claims = new[]
        {
            new Claim("sub", "1"),
            new Claim("scope", scopeClaim),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext BuildContext(ClaimsPrincipal user)
    {
        var requirements = new IAuthorizationRequirement[] { };
        return new AuthorizationHandlerContext(requirements, user, resource: null);
    }
}
