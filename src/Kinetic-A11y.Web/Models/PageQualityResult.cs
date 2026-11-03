using System.ComponentModel.DataAnnotations.Schema;

namespace Kinetic;

public sealed class PageQualityResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiscoveryRunId { get; set; }
    public DiscoveryRun? DiscoveryRun { get; set; }
    public required string Url { get; set; }
    public bool AccessibilityEnabled { get; set; }
    public int? AccessibilityScore { get; set; }
    public int? BestPracticesScore { get; set; }
    public int? BrokenLinksScore { get; set; }
    public int? SeoScore { get; set; }
    public string IssuesJson { get; set; } = "[]";
    public string? LinkCheckError { get; set; }

    [NotMapped]
    public double? OverallScore => BrokenLinksScore is null || SeoScore is null ||
        (AccessibilityEnabled && AccessibilityScore is null) ? null :
        AccessibilityEnabled
            ? (AccessibilityScore!.Value + BrokenLinksScore.Value + SeoScore.Value) / 3d
            : (BrokenLinksScore.Value + SeoScore.Value) / 2d;
}

public sealed record QualityIssue(string Category, string RuleId, string Message, string? Target = null);

public sealed class SiteQualitySummary(IEnumerable<PageQualityResult> results)
{
    private readonly List<PageQualityResult> _pages = results.ToList();
    public int Pages => _pages.Count;
    public int FullyScoredPages => _pages.Count(x => x.OverallScore != null);
    public double? Accessibility => Average(_pages.Select(x => x.AccessibilityScore));
    public double? BestPractices => Average(_pages.Select(x => x.BestPracticesScore));
    public double? BrokenLinks => Average(_pages.Select(x => x.BrokenLinksScore));
    public double? Seo => Average(_pages.Select(x => x.SeoScore));
    public double? Overall => Pages == 0 || BrokenLinks is null || Seo is null ||
        (_pages.Any(x => x.AccessibilityEnabled) && Accessibility is null) ? null :
        _pages.Any(x => x.AccessibilityEnabled)
            ? (Accessibility!.Value + BrokenLinks.Value + Seo.Value) / 3d
            : (BrokenLinks.Value + Seo.Value) / 2d;

    private static double? Average(IEnumerable<int?> scores)
    {
        var values = scores.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
