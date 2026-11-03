namespace Kinetic;

public sealed class CrawlerOptions
{
    public int MaxDepth { get; set; } = 5;
    public int MaxPages { get; set; } = 2000;
    public int MaxConcurrentRequests { get; set; } = 3;
    public int DelayMilliseconds { get; set; } = 500;
    public int RequestTimeoutSeconds { get; set; } = 25;
    public string[] AllowedHosts { get; set; } = [];
    public bool CheckSitemaps { get; set; } = true;
}
