using System.Security.Cryptography.X509Certificates;
using IdentityServer;
using IdentityServer.Extensions;
using IdentityServer.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddRazorPages();

builder.Services.AddScoped<AccountLockoutService>();
builder.Services.AddLoginRateLimiting();

var configuredClients = builder.Configuration
    .GetSection("IdentityServer:Clients")
    .Get<List<IdentityServerClientOptions>>()
    ?? throw new InvalidOperationException("IdentityServer:Clients configuration section is missing.");

if (configuredClients.Count == 0)
{
    throw new InvalidOperationException("IdentityServer:Clients must contain at least one configured client.");
}

var idsBuilder = builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;

    options.UserInteraction.LoginUrl = "/Account/Login";
    options.UserInteraction.LogoutUrl = "/Account/Logout";
})
.AddInMemoryIdentityResources(Config.IdentityResources)
.AddInMemoryApiScopes(Config.ApiScopes)
.AddInMemoryClients(Config.Clients(configuredClients, builder.Environment.IsDevelopment()))
.AddProfileService<CustomProfileService>();

if (builder.Environment.IsDevelopment())
{
    idsBuilder.AddDeveloperSigningCredential();
}
else
{
    var keyPath = builder.Configuration["IdentityServer:SigningCredential:KeyPath"]
        ?? throw new InvalidOperationException(
            "IdentityServer:SigningCredential:KeyPath is required in production");
    var keyPassword = builder.Configuration["IdentityServer:SigningCredential:Password"];
    idsBuilder.AddSigningCredential(X509CertificateLoader.LoadPkcs12FromFile(keyPath, keyPassword));
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
