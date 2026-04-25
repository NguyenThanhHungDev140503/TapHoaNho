using FluentAssertions;
using IdentityServer;

namespace Tests.Unit.IdentityServer;

public class ConfigTests
{
    [Fact]
    public void Clients_InProduction_OnlyReturnsReactDpopClient()
    {
        var clients = Config.Clients(isDevelopment: false).ToList();

        clients.Should().HaveCount(1);
        clients[0].ClientId.Should().Be("react-dpop");
    }

    [Fact]
    public void Clients_InDevelopment_ReturnsBothClients()
    {
        var clients = Config.Clients(isDevelopment: true).ToList();

        clients.Should().HaveCount(2);
        clients.Select(c => c.ClientId).Should().Contain(["react-dpop", "swagger-ui"]);
    }

    [Fact]
    public void ReactDpopClient_RequiresDPoP()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequireDPoP.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_RequiresPkce()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequirePkce.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_IsPublicClient()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequireClientSecret.Should().BeFalse();
    }

    [Fact]
    public void ReactDpopClient_HasShortAccessTokenLifetime()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        // 15 min — short-lived tokens are safe with DPoP (sender-constrained)
        client.AccessTokenLifetime.Should().Be(900);
    }

    [Fact]
    public void ReactDpopClient_UsesOneTimeOnlyRefreshTokens()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RefreshTokenUsage.Should().Be(Duende.IdentityServer.Models.TokenUsage.OneTimeOnly);
    }

    [Fact]
    public void ReactDpopClient_AllowsOfflineAccess()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.AllowOfflineAccess.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_IncludesRetailApiScope()
    {
        var client = Config.Clients(isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.AllowedScopes.Should().Contain("retail-api");
    }

    [Fact]
    public void SwaggerUiClient_DoesNotRequireDPoP()
    {
        var client = Config.Clients(isDevelopment: true).Single(c => c.ClientId == "swagger-ui");

        client.RequireDPoP.Should().BeFalse();
    }

    [Fact]
    public void ApiScopes_ContainsRetailApi()
    {
        Config.ApiScopes.Should().Contain(s => s.Name == "retail-api");
    }

    [Fact]
    public void IdentityResources_ContainsOpenIdAndProfile()
    {
        var names = Config.IdentityResources.Select(r => r.Name).ToList();

        names.Should().Contain("openid");
        names.Should().Contain("profile");
    }
}
