using IdentityServer;
using IdentityServer.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer;

public static class HostingExtensions
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentityServer(options =>
        {
            options.Events.RaiseErrorEvents = true;
            options.Events.RaiseInformationEvents = true;
            options.Events.RaiseFailureEvents = true;
            options.Events.RaiseSuccessEvents = true;
        })
        .AddInMemoryIdentityResources(Config.IdentityResources)
        .AddInMemoryApiScopes(Config.ApiScopes)
        .AddInMemoryClients(Config.Clients)
        .AddProfileService<CustomProfileService>()
        .AddResourceOwnerValidator<CustomResourceOwnerPasswordValidator>();

        return services;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseIdentityServer();

        app.UseRouting();
        app.UseAuthorization();

        return app;
    }
}
