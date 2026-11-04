using Microsoft.Extensions.Options;

namespace Kinetic;

/// <summary>Stores highlighted issue screenshots outside the web root and removes them with their run.</summary>
public sealed class IssuePreviewStore(
    IOptions<AccessibilityOptions> options,
    IWebHostEnvironment environment,
    ILogger<IssuePreviewStore> logger)
{
    private readonly string _rootDirectory = Path.GetFullPath(
        options.Value.IssuePreviewDirectory, environment.ContentRootPath);

    /// <summary>Returns the generated relative path for a run's violation preview.</summary>
    public string GetRelativePath(Guid runId, Guid violationId) =>
        Path.Combine(runId.ToString("N"), $"{violationId:N}.png");

    /// <summary>Returns the full path for a generated run and violation identifier.</summary>
    public string GetFullPath(Guid runId, Guid violationId) =>
        Path.Combine(_rootDirectory, runId.ToString("N"), $"{violationId:N}.png");

    /// <summary>Saves a PNG preview beneath the directory for its discovery run.</summary>
    public async Task SaveAsync(Guid runId, Guid violationId, byte[] image, CancellationToken ct)
    {
        var path = GetFullPath(runId, violationId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, image, ct);
    }

    /// <summary>Attempts to remove all preview images associated with a run.</summary>
    public void DeleteRun(Guid runId)
    {
        var directory = Path.Combine(_rootDirectory, runId.ToString("N"));
        if (!Directory.Exists(directory)) return;

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to delete issue previews for discovery run {RunId} at {Directory}",
                runId, directory);
        }
    }
}
