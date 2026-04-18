using Application;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Log environment info
var environment = builder.Environment.EnvironmentName;
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
logger.LogInformation("=== Clean Architecture + CQRS Configuration Loading ===");
logger.LogInformation($"Environment: {environment}");
logger.LogInformation($"Content Root: {builder.Environment.ContentRootPath}");

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// ================================
// CORS Configuration
// ================================
var corsSettings = builder.Configuration.GetSection("CorsSettings");
var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // DPoP uses Authorization header, not credentials/cookies.
                // Expose:
                //   DPoP-Nonce      — server-issued nonce for replay hardening
                //   WWW-Authenticate — DPoP challenge (needed by SPA to read
                //                      the `dpop-nonce` parameter on 401;
                //                      Safari historically required explicit
                //                      exposure)
                .WithExposedHeaders("DPoP-Nonce", "WWW-Authenticate");
        });
});

// ================================
// Clean Architecture DI
// ================================
builder.Services
    .AddApplication()           // MediatR, Validators, AutoMapper
    .AddInfrastructure(builder.Configuration);  // EF Core, Repositories, Services

// ================================
// Exception Handler
// ================================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ================================
// Controllers
// ================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ================================
// Authentication - DPoP (via IdentityServer)
// ================================
// The API trusts tokens issued by IdentityServer (Authority). Signing keys are
// fetched from the /.well-known/openid-configuration discovery document —
// no shared secret, no SymmetricSecurityKey.
const string DPoPScheme = "dpoptokenscheme";
var identityServerAuthority = builder.Configuration["IdentityServer:Authority"]
    ?? "https://localhost:5001";

builder.Services.AddAuthentication(DPoPScheme)
    .AddJwtBearer(DPoPScheme, options =>
    {
        options.Authority = identityServerAuthority;

        // Audience validation is OFF because Duende v7 issues tokens with
        // scope-based audience (aud = scope name via ApiResource). We validate
        // the scope claim explicitly in the authorization policy instead.
        options.TokenValidationParameters.ValidateAudience = false;

        // at+jwt = RFC 9068 "JWT Profile for OAuth 2.0 Access Tokens"
        // Duende IdentityServer issues tokens with this typ.
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];

        // Keep "role" as-is instead of mapping to ClaimTypes.Role — we shape
        // claims with JwtClaimTypes in IdentityServer's CustomProfileService.
        options.MapInboundClaims = false;

        // Dev only: allow HTTP metadata so localhost IdentityServer works
        // without a trusted cert on the discovery call.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

// Extends the "dpoptokenscheme" above with DPoP proof validation:
//  - Verifies DPoP proof JWT signature (ES256/ES384/PS256...)
//  - Validates htm/htu/iat/jti/ath claims
//  - Binds proof.jkt to access_token.cnf.jkt (proof-of-possession)
//  - Replay-detects via IDistributedCache (see below)
builder.Services.ConfigureDPoPTokensForScheme(DPoPScheme, opt =>
{
    // How far in the past a proof's iat may be. Default 60s.
    opt.ProofTokenIssuedAtClockSkew = TimeSpan.FromSeconds(30);

    // Accept both DPoP-bound tokens and plain Bearer during migration.
    // TODO: set to false after Phase 3 is complete and frontend always
    // sends DPoP proofs. Tokens with cnf.jkt claim always require DPoP
    // regardless of this setting — this only controls unconstrained Bearer.
    opt.AllowBearerTokens = true;

    // Replay protection — reject proofs we've seen before (jti tracking).
    opt.EnableReplayDetection = true;
});

// Required by DPoP replay protection — stores jti of recently-seen proofs.
// In-memory is acceptable for a single-instance dev deployment. For multi-
// instance production, swap in Redis/SQL.
builder.Services.AddDistributedMemoryCache();

builder.Services.AddAuthorization(options =>
{
    // Any authenticated caller must present a token containing the retail-api
    // scope. Role checks stay on controllers via [Authorize(Roles="Admin")].
    //
    // RFC 9068 (JWT Profile for OAuth 2.0 Access Tokens) requires the `scope`
    // claim to be a SINGLE space-separated string ("openid profile retail-api"),
    // not a JSON array. RequireClaim does exact match, which fails. We must
    // split the claim manually.
    options.AddPolicy("RetailApi", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("scope")
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains("retail-api"));
    });

    // Make "RetailApi" the fallback for every endpoint — equivalent to
    // decorating every controller with [Authorize(Policy="RetailApi")].
    // [AllowAnonymous] still short-circuits this (verified by AuthorizationMiddleware).
    options.FallbackPolicy = options.GetPolicy("RetailApi");
});

// ================================
// Swagger
// ================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Retail Store Management API",
        Version = "v1",
        Description = "API cho hệ thống quản lý cửa hàng bán lẻ - Clean Architecture + CQRS + DPoP"
    });

    // OAuth2 flow via IdentityServer — Swagger UI can perform Auth Code + PKCE
    // so testers get a real DPoP-bound token. Note: Swashbuckle's built-in UI
    // does NOT generate DPoP proofs, so protected endpoints will fail until
    // Phase 3 exposes a dedicated DPoP client. Kept here for Bearer fallback.
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{identityServerAuthority}/connect/authorize"),
                TokenUrl = new Uri($"{identityServerAuthority}/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID identifier" },
                    { "profile", "User profile" },
                    { "retail-api", "Access Retail Store API" }
                }
            }
        }
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "retail-api" }
        }
    });
});

var app = builder.Build();

// ================================
// Middleware Pipeline
// ================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RetailStore API v1");
        // Use the dedicated "swagger-ui" IdentityServer client (no DPoP required).
        // Tokens issued to this client are plain Bearer — valid only because the
        // API runs in DPoPAndBearer mode during migration (AllowBearerTokens=true).
        c.OAuthClientId("swagger-ui");
        c.OAuthUsePkce();
    });

    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/")
        {
            context.Response.Redirect("/swagger");
            return;
        }
        await next();
    });
}

app.UseCors(MyAllowSpecificOrigins);
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

logger.LogInformation("=== Application Started Successfully ===");

app.Run();
