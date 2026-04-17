using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new ApiScope("retail-api", "Retail Store API")
        };

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            new Client
            {
                ClientId = "react-dpop",
                ClientName = "React DPoP Client",
                ClientUri = "http://localhost:5173",

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,

                RedirectUris =
                {
                    "http://localhost:5173/callback"
                },
                PostLogoutRedirectUris =
                {
                    "http://localhost:5173"
                },
                AllowedCorsOrigins =
                {
                    "http://localhost:5173"
                },

                AllowedScopes =
                {
                    "openid",
                    "profile",
                    "retail-api",
                    "offline_access"
                },

                AllowOfflineAccess = true,
                AccessTokenLifetime = 3600, // 1 hour
                RefreshTokenExpiration = TokenExpiration.Absolute,
                RefreshTokenUsage = TokenUsage.ReUse
            }
        };
}
