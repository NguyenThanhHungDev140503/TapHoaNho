using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Extensions;

namespace Tests.Unit.WebApi;

public class RateLimitPolicyTests
{
    [Fact]
    public void AddSetupRateLimiting_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();
        var act = () => services.AddSetupRateLimiting();

        act.Should().NotThrow();
    }
}
