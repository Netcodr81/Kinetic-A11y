using System.ComponentModel.DataAnnotations.Schema;

namespace Kinetic;

/// <summary>Stores the page-level category scores and quality issues for one discovery run.</summary>
public sealed class PageQualityResult
{
    /// <summary>Gets or sets the unique identifier for this result.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the discovery run that produced this result.</summary>
    public Guid DiscoveryRunId { get; set; }

    /// <summary>Gets or sets the discovery run that produced this result.</summary>
    public DiscoveryRun? DiscoveryRun { get; set; }

    /// <summary>Gets or sets the normalized URL of the page.</summary>
    public required string Url { get; set; }

    /// <summary>Gets or sets whether accessibility scanning was enabled for this page.</summary>
    public bool AccessibilityEnabled { get; set; }

    /// <summary>Gets or sets the accessibility score, or null if it is unavailable.</summary>
    public int? AccessibilityScore { get; set; }

    /// <summary>Gets or sets the best-practices score, or null if it is unavailable.</summary>
    public int? BestPracticesScore { get; set; }

    /// <summary>Gets or sets the broken-links score, or null if link checking is incomplete or inconclusive.</summary>
    public int? BrokenLinksScore { get; set; }

    /// <summary>Gets or sets the SEO score, or null if it is unavailable.</summary>
    public int? SeoScore { get; set; }

    /// <summary>Gets or sets the JSON-serialized list of quality issues for this page.</summary>
    public string IssuesJson { get; set; } = "[]";

    /// <summary>Gets or sets any error that prevented reliable link-check scoring.</summary>
    public string? LinkCheckError { get; set; }

    /// <summary>Gets the mean of enabled category scores when all required scores are available.</summary>
    /// <remarks>
    /// Accessibility participates only when enabled. SEO and broken-link scores are always required;
    /// a missing score makes the overall page score unavailable rather than treating that check as a pass.
    /// Best Practices is reported separately and is not included in this average.
    /// </remarks>
    [NotMapped]
    public double? OverallScore => BrokenLinksScore is null || SeoScore is null ||
        (AccessibilityEnabled && AccessibilityScore is null) ? null :
        AccessibilityEnabled
            ? (AccessibilityScore!.Value + BrokenLinksScore.Value + SeoScore.Value) / 3d
            : (BrokenLinksScore.Value + SeoScore.Value) / 2d;
}

/// <summary>Describes one quality finding detected on a page.</summary>
/// <param name="Category">The quality category containing the finding.</param>
/// <param name="RuleId">A stable identifier for the rule or issue type.</param>
/// <param name="Message">A human-readable explanation of the finding.</param>
/// <param name="Target">An optional DOM target associated with the finding.</param>
public sealed record QualityIssue(string Category, string RuleId, string Message, string? Target = null);

/// <summary>Calculates aggregate page counts and category averages for a site scan.</summary>
public sealed class SiteQualitySummary(IEnumerable<PageQualityResult> results)
{
    private readonly List<PageQualityResult> _pages = results.ToList();

    /// <summary>Gets the number of page results included in this summary.</summary>
    public int Pages => _pages.Count;

    /// <summary>Gets the number of pages with an available overall score.</summary>
    public int FullyScoredPages => _pages.Count(x => x.OverallScore != null);

    /// <summary>Gets the arithmetic mean of available accessibility scores.</summary>
    public double? Accessibility => Average(_pages.Select(x => x.AccessibilityScore));

    /// <summary>Gets the arithmetic mean of available best-practices scores.</summary>
    public double? BestPractices => Average(_pages.Select(x => x.BestPracticesScore));

    /// <summary>Gets the arithmetic mean of available broken-links scores.</summary>
    public double? BrokenLinks => Average(_pages.Select(x => x.BrokenLinksScore));

    /// <summary>Gets the arithmetic mean of available SEO scores.</summary>
    public double? Seo => Average(_pages.Select(x => x.SeoScore));

    /// <summary>
    /// Gets the mean of site category averages when each enabled category has at least one score.
    /// Accessibility is excluded when no page has accessibility scanning enabled.
    /// </summary>
    /// <remarks>
    /// Each category average uses the pages with a score for that category; failed or unfinished
    /// checks are omitted rather than counted as zero. The overall site score is unavailable until
    /// every enabled category has at least one successful result.
    /// </remarks>
    public double? Overall => Pages == 0 || BrokenLinks is null || Seo is null ||
        (_pages.Any(x => x.AccessibilityEnabled) && Accessibility is null) ? null :
        _pages.Any(x => x.AccessibilityEnabled)
            ? (Accessibility!.Value + BrokenLinks.Value + Seo.Value) / 3d
            : (BrokenLinks.Value + Seo.Value) / 2d;

    private static double? Average(IEnumerable<int?> scores)
    {
        // A missing score means that category was not successfully checked for that page.
        var values = scores.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
