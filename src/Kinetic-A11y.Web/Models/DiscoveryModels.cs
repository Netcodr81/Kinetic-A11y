namespace Kinetic;

public enum DiscoveryRunStatus { Queued, Running, Completed, Failed, Cancelled }
public enum DiscoveredPdfStatus { Pending, Valid, Unverified, Invalid, Unavailable }

public sealed class DiscoveryRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string RootUrl { get; set; }
    public int MaxDepth { get; set; } = 5;
    public int MaxPages { get; set; } = 2000;
    public DiscoveryRunStatus Status { get; set; } = DiscoveryRunStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int PagesDiscovered { get; set; }
    public int PagesCrawled { get; set; }
    public int PdfsDiscovered { get; set; }
    public int PdfsValidated { get; set; }
    public int FailedRequests { get; set; }
    public string? Error { get; set; }
    public bool ScanAccessibility { get; set; } = true;
    public int PagesAccessibilityScanned { get; set; }
    public int AccessibilityScanFailures { get; set; }
    public double? AverageAccessibilityScore { get; set; }
    public List<DiscoveredPdf> Documents { get; set; } = [];
    public List<PageAccessibilityResult> AccessibilityResults { get; set; } = [];
}

public sealed class PageAccessibilityResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiscoveryRunId { get; set; }
    public DiscoveryRun? DiscoveryRun { get; set; }
    public required string Url { get; set; }
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? Score { get; set; }
    public int CriticalCount { get; set; }
    public int SeriousCount { get; set; }
    public int ModerateCount { get; set; }
    public int MinorCount { get; set; }
    public int ViolationCount { get; set; }
    public int PassCount { get; set; }
    public int IncompleteCount { get; set; }
    public string? Error { get; set; }
    public List<AccessibilityViolation> Violations { get; set; } = [];
}

public sealed class AccessibilityViolation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageAccessibilityResultId { get; set; }
    public PageAccessibilityResult? PageAccessibilityResult { get; set; }
    public required string RuleId { get; set; }
    public string? Impact { get; set; }
    public string? Description { get; set; }
    public string? Help { get; set; }
    public string? HelpUrl { get; set; }
    public int NodeCount { get; set; }
    public string? SampleTarget { get; set; }
    public string? SampleHtml { get; set; }
}

public sealed class DiscoveredPdf
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiscoveryRunId { get; set; }
    public DiscoveryRun? DiscoveryRun { get; set; }
    public required string Url { get; set; }
    public required string SourcePageUrl { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTimeOffset FirstDiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public DiscoveredPdfStatus Status { get; set; } = DiscoveredPdfStatus.Pending;
    public string DiscoveryMethod { get; set; } = "HtmlLink";
    public string? ValidationMessage { get; set; }
}
