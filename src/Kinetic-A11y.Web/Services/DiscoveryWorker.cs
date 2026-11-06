namespace Kinetic;

/// <summary>Consumes queued discovery runs and executes them in scoped service instances.</summary>
public sealed class DiscoveryWorker(
    DiscoveryQueue queue,
    DiscoveryRunControl runControl,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscoveryWorker> logger) : BackgroundService
{
    /// <summary>Continuously dequeues runs until the hosted service is asked to stop.</summary>
    /// <param name="stoppingToken">The host-shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid id;
            try { id = await queue.DequeueAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var crawler = scope.ServiceProvider.GetRequiredService<WebsiteCrawler>();
                await runControl.ExecuteAsync(id, ct => crawler.CrawlAsync(id, ct), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Discovery run {RunId} failed", id); }
        }
    }
}
