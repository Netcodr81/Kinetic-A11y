namespace Kinetic;

/// <summary>Configuration for page crawling, request pacing, and sitemap discovery.</summary>
public sealed class CrawlerOptions
{
    /// <summary>Gets or sets the default maximum link depth for a run.</summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>Gets or sets the default maximum number of pages for a run.</summary>
    public int MaxPages { get; set; } = 2000;

    /// <summary>Gets or sets the maximum number of concurrent background checks per run.</summary>
    public int MaxConcurrentRequests { get; set; } = 3;

    /// <summary>Gets or sets the delay between page requests, in milliseconds.</summary>
    public int DelayMilliseconds { get; set; } = 500;

    /// <summary>Gets or sets the timeout for crawler HTTP requests, in seconds.</summary>
    public int RequestTimeoutSeconds { get; set; } = 25;

    /// <summary>Gets or sets additional hosts the crawler may follow beyond the root host.</summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>Gets or sets whether robots.txt and common sitemap URLs should be inspected.</summary>
    public bool CheckSitemaps { get; set; } = true;
}
