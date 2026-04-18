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
            },

            // Swagger UI testing client — same Auth Code + PKCE flow as react-dpop
            // BUT without DPoP, because Swashbuckle UI cannot generate DPoP proofs.
            // This client is dev-only and issues plain Bearer tokens which the API
            // accepts via AllowBearerTokens = true.
            //
            // IMPORTANT: Tokens from this client should NOT be trusted for
            // production traffic. In Phase 4 when DPoP is mandatory, remove this
            // client OR restrict it to a "dev" scope the API refuses in prod.
            new Client
            {
                ClientId = "swagger-ui",
                ClientName = "Swagger UI (dev testing)",

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RequireDPoP = false, // Swashbuckle can't generate proofs

                RedirectUris =
                {
                    // Swagger UI OAuth redirect
                    "https://localhost:5175/swagger/oauth2-redirect.html",
                    "http://localhost:5175/swagger/oauth2-redirect.html"
                },
                AllowedCorsOrigins =
                {
                    "https://localhost:5175",
                    "http://localhost:5175"
                },
                AllowedScopes =
                {
                    "openid",
                    "profile",
                    "retail-api"
                },

                AccessTokenLifetime = 3600 // 1h for easier debugging
            }
        };
}
