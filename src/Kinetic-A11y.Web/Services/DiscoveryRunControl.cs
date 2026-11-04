using Microsoft.EntityFrameworkCore;

namespace Kinetic;

/// <summary>Coordinates discovery-run execution, cancellation, deletion, and restart.</summary>
public sealed class DiscoveryRunControl(
    IDbContextFactory<DiscoveryDbContext> dbFactory,
    DiscoveryQueue queue,
    IssuePreviewStore previews,
    ILogger<DiscoveryRunControl> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, (CancellationTokenSource Cancellation, TaskCompletionSource Finished)> _active = [];

    /// <summary>Cancels a queued or active run and waits for active work to finish.</summary>
    /// <param name="runId">The identifier of the run to cancel.</param>
    public async Task CancelAsync(Guid runId)
    {
        Task finished = Task.CompletedTask;
        await _gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var run = await db.DiscoveryRuns.SingleOrDefaultAsync(x => x.Id == runId)
                ?? throw new InvalidOperationException("This discovery run no longer exists.");
            if (_active.TryGetValue(runId, out var active))
            {
                active.Cancellation.Cancel();
                finished = active.Finished.Task;
            }
            else if (run.Status is DiscoveryRunStatus.Queued or DiscoveryRunStatus.Running)
            {
                run.Status = DiscoveryRunStatus.Cancelled;
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.CurrentActivity = null;
                await db.SaveChangesAsync();
            }
        }
        finally { _gate.Release(); }
        await finished;
    }

    /// <summary>Stops a run if necessary, then deletes it and its related results.</summary>
    /// <param name="runId">The identifier of the run to delete.</param>
    public async Task DeleteAsync(Guid runId)
    {
        // Wait for the worker to stop writing before deleting its run and results.
        await CancelAsync(runId);
        await _gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var run = await db.DiscoveryRuns.SingleOrDefaultAsync(x => x.Id == runId)
                ?? throw new InvalidOperationException("This discovery run no longer exists.");
            db.DiscoveryRuns.Remove(run);
            await db.SaveChangesAsync();
            previews.DeleteRun(runId);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Replaces a stopped run with a fresh run using its saved crawl settings.</summary>
    /// <param name="runId">The identifier of the run to replace.</param>
    /// <returns>The identifier of the newly queued run.</returns>
    public async Task<Guid> RestartAsync(Guid runId)
    {
        await _gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var previous = await db.DiscoveryRuns.SingleOrDefaultAsync(x => x.Id == runId)
                ?? throw new InvalidOperationException("This discovery run no longer exists.");
            if (_active.ContainsKey(runId) || previous.Status is DiscoveryRunStatus.Queued or DiscoveryRunStatus.Running)
            {
                throw new InvalidOperationException("Cancel the scan before starting it again.");
            }

            var replacement = new DiscoveryRun
            {
                RootUrl = previous.RootUrl,
                MaxDepth = previous.MaxDepth,
                MaxPages = previous.MaxPages,
                ScanAccessibility = previous.ScanAccessibility,
                RenderJavaScript = previous.RenderJavaScript,
                JavaScriptWaitSeconds = previous.JavaScriptWaitSeconds
            };
            db.DiscoveryRuns.Remove(previous);
            db.DiscoveryRuns.Add(replacement);
            // SaveChanges deletes the old results and inserts the replacement in one transaction.
            await db.SaveChangesAsync();
            previews.DeleteRun(runId);
            await queue.EnqueueAsync(replacement.Id);
            return replacement.Id;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Marks a queued run as active, invokes its crawl operation, and persists its final state.</summary>
    /// <param name="runId">The identifier of the run to execute.</param>
    /// <param name="crawl">The asynchronous crawl operation, supplied with the run's cancellation token.</param>
    /// <param name="stoppingToken">The host-shutdown token linked to the run's cancellation.</param>
    public async Task ExecuteAsync(Guid runId, Func<CancellationToken, Task> crawl, CancellationToken stoppingToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _gate.WaitAsync(stoppingToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
            var run = await db.DiscoveryRuns.SingleOrDefaultAsync(x => x.Id == runId, stoppingToken);
            if (run is null || run.Status != DiscoveryRunStatus.Queued)
            {
                return;
            }

            run.Status = DiscoveryRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            _active.Add(runId, (cancellation, finished));
        }
        finally { _gate.Release(); }

        var status = DiscoveryRunStatus.Completed;
        string? error = null;
        try
        {
            await crawl(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            status = DiscoveryRunStatus.Cancelled;
        }
        catch (Exception ex)
        {
            status = DiscoveryRunStatus.Failed;
            error = ex.Message;
            logger.LogError(ex, "Discovery run {RunId} failed", runId);
        }
        finally
        {
            await _gate.WaitAsync();
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                var run = await db.DiscoveryRuns.SingleAsync(x => x.Id == runId);
                run.Status = cancellation.IsCancellationRequested ? DiscoveryRunStatus.Cancelled : status;
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.Error = error ?? run.Error;
                run.CurrentActivity = null;
                await db.SaveChangesAsync();
                finished.SetResult();
            }
            catch (Exception ex)
            {
                finished.SetException(ex);
                throw;
            }
            finally
            {
                _active.Remove(runId);
                _gate.Release();
            }
        }
    }
}
