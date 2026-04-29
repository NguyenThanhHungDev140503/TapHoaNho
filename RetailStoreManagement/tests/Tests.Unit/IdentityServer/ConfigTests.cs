using FluentAssertions;
using IdentityServer;

namespace Tests.Unit.IdentityServer;

public class ConfigTests
{
    private static List<IdentityServerClientOptions> CreateConfiguredClients() =>
    [
        new()
        {
            ClientId = "react-dpop",
            ClientName = "React DPoP Client",
            ClientUri = "http://localhost:5173",
            AllowedGrantTypes = ["code"],
            RequirePkce = true,
            RequireClientSecret = false,
            RequireDPoP = true,
            RedirectUris = ["http://localhost:5173/callback"],
            PostLogoutRedirectUris = ["http://localhost:5173"],
            AllowedCorsOrigins = ["http://localhost:5173"],
            AllowedScopes = ["openid", "profile", "retail-api", "offline_access"],
            AllowOfflineAccess = true,
            AccessTokenLifetime = 900,
            RefreshTokenExpiration = "Sliding",
            RefreshTokenUsage = "OneTimeOnly",
            SlidingRefreshTokenLifetime = 60 * 60 * 24 * 7,
            AbsoluteRefreshTokenLifetime = 60 * 60 * 24 * 30,
            DevelopmentOnly = false
        },
        new()
        {
            ClientId = "swagger-ui",
            ClientName = "Swagger UI (dev testing)",
            AllowedGrantTypes = ["code"],
            RequirePkce = true,
            RequireClientSecret = false,
            RequireDPoP = false,
            RedirectUris =
            [
                "https://localhost:5175/swagger/oauth2-redirect.html",
                "http://localhost:5175/swagger/oauth2-redirect.html"
            ],
            AllowedCorsOrigins = ["https://localhost:5175", "http://localhost:5175"],
            AllowedScopes = ["openid", "profile", "retail-api"],
            AccessTokenLifetime = 3600,
            DevelopmentOnly = true
        }
    ];

    [Fact]
    public void Clients_ThrowsForUnsupportedGrantType()
    {
        var configuredClients = new List<IdentityServerClientOptions>
        {
            new()
            {
                ClientId = "invalid-client",
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = ["retail-api"]
            }
        };

        var action = () => Config.Clients(configuredClients, isDevelopment: true).ToList();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only 'code' is supported.*");
    }

    [Fact]
    public void Clients_InProduction_FiltersDevelopmentOnlyClients()
    {
        var clients = Config.Clients(CreateConfiguredClients(), isDevelopment: false).ToList();

        clients.Should().OnlyContain(c => c.ClientId != "swagger-ui");
    }
    [Fact]
    public void Clients_InProduction_OnlyReturnsReactDpopClient()
    {
        var clients = Config.Clients(CreateConfiguredClients(), isDevelopment: false).ToList();

        clients.Should().HaveCount(1);
        clients[0].ClientId.Should().Be("react-dpop");
    }

    [Fact]
    public void Clients_InDevelopment_ReturnsBothClients()
    {
        var clients = Config.Clients(CreateConfiguredClients(), isDevelopment: true).ToList();

        clients.Should().HaveCount(2);
        clients.Select(c => c.ClientId).Should().Contain(["react-dpop", "swagger-ui"]);
    }

    [Fact]
    public void ReactDpopClient_RequiresDPoP()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequireDPoP.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_RequiresPkce()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequirePkce.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_IsPublicClient()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RequireClientSecret.Should().BeFalse();
    }

    [Fact]
    public void ReactDpopClient_HasShortAccessTokenLifetime()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        // 15 min — short-lived tokens are safe with DPoP (sender-constrained)
        client.AccessTokenLifetime.Should().Be(900);
    }

    [Fact]
    public void ReactDpopClient_UsesOneTimeOnlyRefreshTokens()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.RefreshTokenUsage.Should().Be(Duende.IdentityServer.Models.TokenUsage.OneTimeOnly);
    }

    [Fact]
    public void ReactDpopClient_AllowsOfflineAccess()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.AllowOfflineAccess.Should().BeTrue();
    }

    [Fact]
    public void ReactDpopClient_IncludesRetailApiScope()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: false).Single(c => c.ClientId == "react-dpop");

        client.AllowedScopes.Should().Contain("retail-api");
    }

    [Fact]
    public void SwaggerUiClient_DoesNotRequireDPoP()
    {
        var client = Config.Clients(CreateConfiguredClients(), isDevelopment: true).Single(c => c.ClientId == "swagger-ui");

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
