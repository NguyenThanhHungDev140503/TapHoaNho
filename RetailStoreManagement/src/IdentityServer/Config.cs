using Duende.IdentityServer.Models;

namespace IdentityServer;

public sealed class IdentityServerClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? ClientUri { get; init; }
    public List<string> AllowedGrantTypes { get; init; } = [];
    public bool RequirePkce { get; init; }
    public bool RequireClientSecret { get; init; }
    public bool RequireDPoP { get; init; }
    public List<string> RedirectUris { get; init; } = [];
    public List<string> PostLogoutRedirectUris { get; init; } = [];
    public List<string> AllowedCorsOrigins { get; init; } = [];
    public List<string> AllowedScopes { get; init; } = [];
    public bool AllowOfflineAccess { get; init; }
    public int AccessTokenLifetime { get; init; }
    public string? RefreshTokenExpiration { get; init; }
    public string? RefreshTokenUsage { get; init; }
    public int SlidingRefreshTokenLifetime { get; init; }
    public int AbsoluteRefreshTokenLifetime { get; init; }
    public bool DevelopmentOnly { get; init; }
}

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
    /// <param name="clientOptions">Configured clients from <c>IdentityServer:Clients</c>.</param>
    /// <param name="isDevelopment">
    /// Value of <c>IHostEnvironment.IsDevelopment()</c> — derived from
    /// ASPNETCORE_ENVIRONMENT which devenv loads from .env.secrets.
    /// </param>
    public static IEnumerable<Client> Clients(IEnumerable<IdentityServerClientOptions> clientOptions, bool isDevelopment)
    {
        foreach (var option in clientOptions)
        {
            if (option.DevelopmentOnly && !isDevelopment)
            {
                continue;
            }

            yield return MapClient(option);
        }
    }

    private static Client MapClient(IdentityServerClientOptions option)
    {
        if (string.IsNullOrWhiteSpace(option.ClientId))
        {
            throw new InvalidOperationException("IdentityServer client configuration must specify clientId.");
        }

        return new Client
        {
            ClientId = option.ClientId,
            ClientName = option.ClientName,
            ClientUri = option.ClientUri,
            AllowedGrantTypes = MapAllowedGrantTypes(option.AllowedGrantTypes, option.ClientId),
            RequirePkce = option.RequirePkce,
            RequireClientSecret = option.RequireClientSecret,
            RequireDPoP = option.RequireDPoP,
            RedirectUris = option.RedirectUris,
            PostLogoutRedirectUris = option.PostLogoutRedirectUris,
            AllowedCorsOrigins = option.AllowedCorsOrigins,
            AllowedScopes = option.AllowedScopes,
            AllowOfflineAccess = option.AllowOfflineAccess,
            AccessTokenLifetime = option.AccessTokenLifetime,
            RefreshTokenExpiration = MapRefreshTokenExpiration(option.RefreshTokenExpiration, option.ClientId),
            RefreshTokenUsage = MapRefreshTokenUsage(option.RefreshTokenUsage, option.ClientId),
            SlidingRefreshTokenLifetime = option.SlidingRefreshTokenLifetime,
            AbsoluteRefreshTokenLifetime = option.AbsoluteRefreshTokenLifetime
        };
    }

    private static ICollection<string> MapAllowedGrantTypes(IEnumerable<string> configuredGrantTypes, string clientId)
    {
        var grantTypes = configuredGrantTypes.ToList();
        if (grantTypes.Count == 0)
        {
            throw new InvalidOperationException($"IdentityServer client '{clientId}' must configure at least one allowedGrantTypes value.");
        }

        if (grantTypes.Any(grant => !string.Equals(grant, "code", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"IdentityServer client '{clientId}' has unsupported allowedGrantTypes. Only 'code' is supported.");
        }

        return GrantTypes.Code;
    }

    private static TokenExpiration MapRefreshTokenExpiration(string? configuredValue, string clientId)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return TokenExpiration.Absolute;
        }

        if (Enum.TryParse<TokenExpiration>(configuredValue, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"IdentityServer client '{clientId}' has invalid refreshTokenExpiration '{configuredValue}'.");
    }

    private static TokenUsage MapRefreshTokenUsage(string? configuredValue, string clientId)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return TokenUsage.ReUse;
        }

        if (Enum.TryParse<TokenUsage>(configuredValue, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"IdentityServer client '{clientId}' has invalid refreshTokenUsage '{configuredValue}'.");
    }
}
