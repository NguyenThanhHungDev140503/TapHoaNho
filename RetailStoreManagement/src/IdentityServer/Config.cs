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

    /// <summary>
    /// OAuth clients. The swagger-ui testing client is only registered in
    /// Development. In Production the API also sets AllowBearerTokens=false,
    /// making plain-Bearer tokens useless even if this client somehow exists.
    /// Defense in depth.
    /// </summary>
    /// <param name="isDevelopment">
    /// Value of <c>IHostEnvironment.IsDevelopment()</c> — derived from
    /// ASPNETCORE_ENVIRONMENT which devenv loads from .env.secrets.
    /// </param>
    public static IEnumerable<Client> Clients(bool isDevelopment)
    {
        yield return new Client
        {
            ClientId = "react-dpop",
            ClientName = "React DPoP Client",
            ClientUri = "http://localhost:5173",

            // OAuth 2.1: Authorization Code only + PKCE required
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false, // public SPA client

            // DPoP: sender-constrained tokens (RFC 9449)
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
            AccessTokenLifetime = 900, // 15 min — DPoP lets us keep tokens short
            RefreshTokenExpiration = TokenExpiration.Sliding,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            SlidingRefreshTokenLifetime = 60 * 60 * 24 * 7,  // 7 days
            AbsoluteRefreshTokenLifetime = 60 * 60 * 24 * 30 // 30 days cap
        };

        // Swagger UI testing client — Development ONLY.
        // In Production this client does not exist, so /connect/authorize
        // with client_id=swagger-ui returns "invalid_client".
        if (isDevelopment)
        {
            yield return new Client
            {
                ClientId = "swagger-ui",
                ClientName = "Swagger UI (dev testing)",

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RequireDPoP = false, // Swashbuckle can't generate proofs

                RedirectUris =
                {
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
            };
        }
    }
}
