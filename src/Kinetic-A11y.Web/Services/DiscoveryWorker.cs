using Microsoft.EntityFrameworkCore;


namespace Kinetic;

public sealed class DiscoveryWorker(
    DiscoveryQueue queue,
    IDbContextFactory<DiscoveryDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid id;
            try { id = await queue.DequeueAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }

            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
                var run = await db.DiscoveryRuns.SingleOrDefaultAsync(x => x.Id == id, stoppingToken);
                if (run is null || run.Status != DiscoveryRunStatus.Queued) continue;
                run.Status = DiscoveryRunStatus.Running;
                run.StartedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var crawler = scope.ServiceProvider.GetRequiredService<WebsiteCrawler>();
                await crawler.CrawlAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Discovery run {RunId} failed", id); }
        }
    }
}
