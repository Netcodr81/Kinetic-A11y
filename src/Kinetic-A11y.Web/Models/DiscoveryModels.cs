namespace Kinetic;

/// <summary>Describes the lifecycle state of a website discovery run.</summary>
public enum DiscoveryRunStatus { Queued, Running, Completed, Failed, Cancelled }

/// <summary>Describes the result of checking a discovered PDF URL.</summary>
public enum DiscoveredPdfStatus { Pending, Valid, Unverified, Invalid, Unavailable }

/// <summary>Stores the configuration, progress, and results for one website discovery run.</summary>
public sealed class DiscoveryRun
{
    /// <summary>Gets or sets the unique identifier for this run.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the absolute HTTP or HTTPS URL at which crawling begins.</summary>
    public required string RootUrl { get; set; }

    /// <summary>Gets or sets the maximum link depth the crawler will visit.</summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>Gets or sets the maximum number of pages the run may discover.</summary>
    public int MaxPages { get; set; } = 2000;

    /// <summary>Gets or sets the current lifecycle state of the run.</summary>
    public DiscoveryRunStatus Status { get; set; } = DiscoveryRunStatus.Queued;

    /// <summary>Gets or sets the UTC time at which this run was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC time at which crawling began, if it has started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the UTC time at which the run reached a terminal state.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the number of unique pages discovered.</summary>
    public int PagesDiscovered { get; set; }

    /// <summary>Gets or sets the number of pages processed by the crawler.</summary>
    public int PagesCrawled { get; set; }

    /// <summary>Gets or sets the number of PDF URLs found.</summary>
    public int PdfsDiscovered { get; set; }

    /// <summary>Gets or sets the number of discovered PDFs whose URLs were checked.</summary>
    public int PdfsValidated { get; set; }

    /// <summary>Gets or sets the number of page requests that failed.</summary>
    public int FailedRequests { get; set; }

    /// <summary>Gets or sets the run-level error message, if the run failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets a human-readable description of the work currently in progress.</summary>
    public string? CurrentActivity { get; set; }

    /// <summary>Gets or sets whether the run should perform accessibility scans.</summary>
    public bool ScanAccessibility { get; set; } = true;

    /// <summary>Gets or sets whether pages should be rendered in Chromium before analysis.</summary>
    public bool RenderJavaScript { get; set; }

    /// <summary>Gets or sets the additional wait after browser navigation, in seconds, for page scripts to render.</summary>
    public int JavaScriptWaitSeconds { get; set; } = 2;

    /// <summary>Gets or sets the number of pages with a successfully calculated accessibility score.</summary>
    public int PagesAccessibilityScanned { get; set; }

    /// <summary>Gets or sets the number of pages whose accessibility scan did not produce a score.</summary>
    public int AccessibilityScanFailures { get; set; }

    /// <summary>Gets or sets the average accessibility score of successfully scored pages.</summary>
    public double? AverageAccessibilityScore { get; set; }

    /// <summary>Gets or sets the PDF documents discovered by this run.</summary>
    public List<DiscoveredPdf> Documents { get; set; } = [];

    /// <summary>Gets or sets the accessibility results recorded for this run.</summary>
    public List<PageAccessibilityResult> AccessibilityResults { get; set; } = [];
}

/// <summary>Stores the accessibility findings and score for one scanned page.</summary>
public sealed class PageAccessibilityResult
{
    /// <summary>Gets or sets the unique identifier for this page result.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the discovery run that produced this result.</summary>
    public Guid DiscoveryRunId { get; set; }

    /// <summary>Gets or sets the discovery run that produced this result.</summary>
    public DiscoveryRun? DiscoveryRun { get; set; }

    /// <summary>Gets or sets the normalized URL of the scanned page.</summary>
    public required string Url { get; set; }

    /// <summary>Gets or sets the UTC time at which the page was scanned.</summary>
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the calculated accessibility score, or null when no score was produced.</summary>
    public int? Score { get; set; }

    /// <summary>Gets or sets the number of distinct critical accessibility rules with violations.</summary>
    public int CriticalCount { get; set; }

    /// <summary>Gets or sets the number of distinct serious accessibility rules with violations.</summary>
    public int SeriousCount { get; set; }

    /// <summary>Gets or sets the number of distinct moderate accessibility rules with violations.</summary>
    public int ModerateCount { get; set; }

    /// <summary>Gets or sets the number of distinct minor accessibility rules with violations.</summary>
    public int MinorCount { get; set; }

    /// <summary>Gets or sets the number of distinct accessibility rules with violations.</summary>
    public int ViolationCount { get; set; }

    /// <summary>Gets or sets the number of axe-core checks that passed.</summary>
    public int PassCount { get; set; }

    /// <summary>Gets or sets the number of checks that axe-core marked incomplete.</summary>
    public int IncompleteCount { get; set; }

    /// <summary>Gets or sets the scan error message, if the scan did not complete successfully.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the stored details for accessibility violations on the page.</summary>
    public List<AccessibilityViolation> Violations { get; set; } = [];

    /// <summary>Gets or sets the non-persisted score for accessibility best-practice findings.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? BestPracticesScore { get; set; }

    /// <summary>Gets or sets the non-persisted combined accessibility and best-practice issue details.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<QualityIssue> QualityIssues { get; set; } = [];
}

/// <summary>Stores a representative element and rule description for an accessibility violation.</summary>
public sealed class AccessibilityViolation
{
    /// <summary>Gets or sets the unique identifier for this violation record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the page result containing this violation.</summary>
    public Guid PageAccessibilityResultId { get; set; }

    /// <summary>Gets or sets the page result containing this violation.</summary>
    public PageAccessibilityResult? PageAccessibilityResult { get; set; }

    /// <summary>Gets or sets the axe-core rule identifier.</summary>
    public required string RuleId { get; set; }

    /// <summary>Gets or sets the reported impact level, when supplied by axe-core.</summary>
    public string? Impact { get; set; }

    /// <summary>Gets or sets the rule description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the help text explaining the rule.</summary>
    public string? Help { get; set; }

    /// <summary>Gets or sets the URL of the rule guidance.</summary>
    public string? HelpUrl { get; set; }

    /// <summary>Gets or sets the number of affected DOM nodes for this rule.</summary>
    public int NodeCount { get; set; }

    /// <summary>Gets or sets a selector or target identifying one representative affected node.</summary>
    public string? SampleTarget { get; set; }

    /// <summary>Gets or sets a truncated HTML snippet for one representative affected node.</summary>
    public string? SampleHtml { get; set; }
}

/// <summary>Stores the discovery source and URL-validation details for a found PDF.</summary>
public sealed class DiscoveredPdf
{
    /// <summary>Gets or sets the unique identifier for this discovered document.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the run that found this document.</summary>
    public Guid DiscoveryRunId { get; set; }

    /// <summary>Gets or sets the discovery run that found this document.</summary>
    public DiscoveryRun? DiscoveryRun { get; set; }

    /// <summary>Gets or sets the URL of the PDF resource.</summary>
    public required string Url { get; set; }

    /// <summary>Gets or sets the page or sitemap URL from which the PDF was discovered.</summary>
    public required string SourcePageUrl { get; set; }

    /// <summary>Gets or sets the filename inferred for the document.</summary>
    public string? FileName { get; set; }

    /// <summary>Gets or sets the content type reported by the server.</summary>
    public string? ContentType { get; set; }

    /// <summary>Gets or sets the content length reported by the server, in bytes.</summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>Gets or sets the UTC time at which this document was first found.</summary>
    public DateTimeOffset FirstDiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC time at which this document was last validated.</summary>
    public DateTimeOffset? LastVerifiedAt { get; set; }

    /// <summary>Gets or sets the outcome of checking the document URL.</summary>
    public DiscoveredPdfStatus Status { get; set; } = DiscoveredPdfStatus.Pending;

    /// <summary>Gets or sets how the document URL was found, such as an HTML link or sitemap.</summary>
    public string DiscoveryMethod { get; set; } = "HtmlLink";

    /// <summary>Gets or sets additional information from the latest validation attempt.</summary>
    public string? ValidationMessage { get; set; }
}
