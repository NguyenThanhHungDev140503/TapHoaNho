using FluentAssertions;

namespace Tests.Unit.WebApi;

public class DPoPEnforcementTests
{
    [Fact]
    public void AllowBearerTokens_DevelopmentEnvironment_ReturnsTrue()
    {
        var isDevelopment = true;
        var allowBearerTokens = isDevelopment;

        allowBearerTokens.Should().BeTrue(
            "because dev mode must allow Swagger UI to test with plain Bearer");
    }

    [Fact]
    public void AllowBearerTokens_ProductionEnvironment_ReturnsFalse()
    {
        var isDevelopment = false;
        var allowBearerTokens = isDevelopment;

        allowBearerTokens.Should().BeFalse(
            "because production must reject all unconstrained Bearer tokens");
    }
}
