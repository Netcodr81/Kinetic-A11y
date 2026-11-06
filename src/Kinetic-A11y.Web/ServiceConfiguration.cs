using Blazilla.Extensions;
using Blazor.Sonner.Extensions;
using BlazorBlueprint.Components;
using Kinetic;
using Kinetic_A11y.Web.Common.Services;
using Kinetic_A11y.Web.Infrastructure;
using Microsoft.Data.Sqlite;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Kinetic_A11y.Web;

public static class ServiceConfiguration
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        // #################################
        // Caching
        // #################################

        services
            .AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromHours(1)
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer());

        // #################################
        // Validation
        // #################################

        services.AddValidatorsFromAssemblyContaining<Program>();

        // #################################
        // Other services
        // #################################

        services.AddBlazorBlueprintComponents();

        services.AddSonner();

        services.AddScoped<LoadingService>();
        
        services.AddCascadingAuthenticationState();

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ##################################
        // Data Access
        // ##################################

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        return services;
    }

    /// <summary>Registers services used by website discovery and scanning.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration containing discovery settings.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddDiscoveryServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<DiscoveryDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Discovery")
                ?? "Data Source=pdf-discovery.db"));

        services.AddHttpClient("crawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("StatePdfDiscoveryMvp/1.0 (+contact administrator)");
        }).AddStandardResilienceHandler();

        services.Configure<CrawlerOptions>(configuration.GetSection("Crawler"));
        services.Configure<AccessibilityOptions>(configuration.GetSection("Accessibility"));
        services.AddSingleton<AccessibilityScanner>();
        services.AddSingleton<IssuePreviewStore>();
        services.AddSingleton<DiscoveryQueue>();
        services.AddSingleton<DiscoveryRunControl>();
        services.AddScoped<WebsiteCrawler>();
        services.AddScoped<PageQualityScanner>();
        services.AddHostedService<DiscoveryWorker>();

        return services;
    }

    /// <summary>Creates the discovery database and applies its additive schema updates.</summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="logger">The application logger used to report schema recovery.</param>
    public static async Task InitializeDiscoveryDatabaseAsync(this IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiscoveryDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            // EnsureCreated does not upgrade existing databases; probe for the newest schema elements.
            _ = await db.DiscoveryRuns.AsNoTracking()
                .Where(x => x.ScanAccessibility && x.AverageAccessibilityScore != null).AnyAsync();
            _ = await db.AccessibilityViolations.AsNoTracking().AnyAsync();
        }
        catch (SqliteException ex)
        {
            logger.LogWarning(ex, "Discovery database schema is out of date; recreating it. Existing runs will be lost.");
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        await db.EnsureQualitySchemaAsync();
        await db.EnsureJavaScriptSchemaAsync();
        await db.EnsureIssuePreviewSchemaAsync();
        await db.EnsureWcagLevelSchemaAsync();
    }
}
