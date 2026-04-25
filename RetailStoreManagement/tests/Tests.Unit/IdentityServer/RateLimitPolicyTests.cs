using FluentAssertions;
using IdentityServer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Unit.IdentityServer;

public class RateLimitPolicyTests
{
    [Fact]
    public void AddLoginRateLimiting_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();
        var act = () => services.AddLoginRateLimiting();

        act.Should().NotThrow();
    }
}
