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

                // OAuth 2.1: Authorization Code only + PKCE required
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false, // public SPA client

                // DPoP: sender-constrained tokens (RFC 9449)
                // Client must prove possession of a key on every token/API call.
                RequireDPoP = true,

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

                // DPoP allows short-lived access tokens safely
                AccessTokenLifetime = 900, // 15 minutes

                // Rotate refresh tokens on every use to mitigate theft
                RefreshTokenExpiration = TokenExpiration.Sliding,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                SlidingRefreshTokenLifetime = 60 * 60 * 24 * 7, // 7 days
                AbsoluteRefreshTokenLifetime = 60 * 60 * 24 * 30 // 30 days cap
            }
        };
}
