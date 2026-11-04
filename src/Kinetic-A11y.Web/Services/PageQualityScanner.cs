using AngleSharp.Dom;
using System.Net;
using System.Text.Json;

namespace Kinetic;

/// <summary>Checks page metadata and outbound links to produce page-quality scores.</summary>
public sealed class PageQualityScanner(ILogger<PageQualityScanner> logger)
{
    private readonly Dictionary<string, (bool? Broken, string? Error)> _linkChecks = new(StringComparer.Ordinal);

    /// <summary>Creates a page result, extracts SEO issues, and checks its links.</summary>
    /// <param name="runId">The discovery run that owns the result.</param>
    /// <param name="url">The page URL represented by the document.</param>
    /// <param name="document">The parsed page document.</param>
    /// <param name="client">The HTTP client used to check link destinations.</param>
    /// <param name="accessibilityEnabled">Whether accessibility is an enabled score category for this page.</param>
    /// <param name="ct">A token that cancels link checking.</param>
    /// <returns>The page-quality result with SEO findings and link-check results.</returns>
    public async Task<PageQualityResult> ScanAsync(Guid runId, Uri url, IDocument document,
        HttpClient client, bool accessibilityEnabled, CancellationToken ct)
    {
        var result = CreateResult(runId, url, document, accessibilityEnabled);
        await ScanLinksAsync(result, url, document, client, ct);
        return result;
    }

    /// <summary>Creates a page-quality result and detects missing or empty SEO metadata.</summary>
    /// <param name="runId">The discovery run that owns the result.</param>
    /// <param name="url">The page URL represented by the document.</param>
    /// <param name="document">The parsed page document.</param>
    /// <param name="accessibilityEnabled">Whether accessibility is an enabled score category for this page.</param>
    /// <returns>A result initialized with its URL, SEO score, and serialized SEO issues.</returns>
    public PageQualityResult CreateResult(Guid runId, Uri url, IDocument document, bool accessibilityEnabled)
    {
        var result = new PageQualityResult
        {
            DiscoveryRunId = runId,
            Url = url.AbsoluteUri,
            AccessibilityEnabled = accessibilityEnabled
        };
        var issues = new List<QualityIssue>();
        if (string.IsNullOrWhiteSpace(document.Title))
            issues.Add(new("SEO", "missing-title", "Page title is missing or empty."));
        if (!document.QuerySelectorAll("meta[name]").Any(x =>
                string.Equals(x.GetAttribute("name"), "description", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.GetAttribute("content"))))
            issues.Add(new("SEO", "missing-description", "Meta description is missing or empty."));

        // SEO scoring currently covers only a nonempty page title and meta description.
        result.SeoScore = AccessibilityScoring.Calculate(issues.Where(x => x.Category == "SEO").Select(x => x.RuleId));
        result.IssuesJson = JsonSerializer.Serialize(issues);
        return result;
    }

    /// <summary>Checks unique HTTP(S) links from a parsed page, resolving relative URLs against its base element.</summary>
    /// <param name="result">The quality result to update.</param>
    /// <param name="url">The URL from which the document was loaded.</param>
    /// <param name="document">The parsed page containing links.</param>
    /// <param name="client">The HTTP client used to check link destinations.</param>
    /// <param name="ct">A token that cancels link checking.</param>
    public Task ScanLinksAsync(PageQualityResult result, Uri url, IDocument document,
        HttpClient client, CancellationToken ct) =>
        ScanLinksAsync(result, GetLinkBase(url, document),
            document.QuerySelectorAll("a[href], area[href]").Select(x => x.GetAttribute("href")!).ToArray(), client, ct);

    /// <summary>Resolves an HTML base URL against the page URL when it is a valid HTTP(S) URL.</summary>
    /// <param name="url">The URL from which the document was loaded.</param>
    /// <param name="document">The parsed document whose base element may override relative-link resolution.</param>
    /// <returns>The valid HTTP(S) base URL, or the original page URL.</returns>
    public static Uri GetLinkBase(Uri url, IDocument document)
    {
        var baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        return Uri.TryCreate(url, baseHref, out var resolvedBase) && resolvedBase.Scheme is "http" or "https"
            ? resolvedBase : url;
    }

    /// <summary>Checks the supplied links and updates the result's broken-link score and issues.</summary>
    /// <param name="result">The quality result to update.</param>
    /// <param name="baseUri">The URL used to resolve relative link targets.</param>
    /// <param name="links">The link references to check.</param>
    /// <param name="client">The HTTP client used to check link destinations.</param>
    /// <param name="ct">A token that cancels link checking.</param>
    public async Task ScanLinksAsync(PageQualityResult result, Uri baseUri, IReadOnlyList<string> links,
        HttpClient client, CancellationToken ct)
    {
        var issues = JsonSerializer.Deserialize<List<QualityIssue>>(result.IssuesJson)
            ?? throw new InvalidOperationException("Invalid quality issues.");
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        // Check each destination once per page; the per-run cache below also avoids repeating
        // network requests for destinations already checked on other pages.
        foreach (var link in links)
        {
            ct.ThrowIfCancellationRequested();
            var href = link.Trim();
            if (href.Length == 0 || href.StartsWith('#')) continue;
            if (!Uri.TryCreate(baseUri, href, out var target))
            {
                issues.Add(new("Broken Links", $"invalid:{href}", "Invalid link URL.", href));
                continue;
            }
            if (target.Scheme is not ("http" or "https")) continue;
            // Fragments identify positions within a page, not separate network resources.
            var key = new UriBuilder(target) { Fragment = "" }.Uri.AbsoluteUri;
            if (!targets.Add(key)) continue;
            if (!_linkChecks.TryGetValue(key, out var check))
            {
                check = await CheckLinkAsync(new Uri(key), client, ct);
                _linkChecks.Add(key, check);
            }
            if (check.Broken == true)
                issues.Add(new("Broken Links", key, check.Error ?? "Broken link.", key));
            else if (check.Broken is null)
                errors.Add($"{key}: {check.Error}");
        }
        result.LinkCheckError = errors.Count == 0 ? null : string.Join("\n", errors);
        // Only confirmed broken URLs lower the score. If any check was inconclusive, leave the
        // category unscored rather than presenting an incomplete check as a successful result.
        result.BrokenLinksScore = errors.Count == 0
            ? AccessibilityScoring.Calculate(issues.Where(x => x.Category == "Broken Links").Select(x => x.RuleId))
            : null;
        result.IssuesJson = JsonSerializer.Serialize(issues.DistinctBy(x => (x.Category, x.RuleId)));
    }

    private async Task<(bool? Broken, string? Error)> CheckLinkAsync(Uri url, HttpClient client, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) return (false, null);
            // HEAD is not consistently supported; confirm failures using GET without reading the body.
            using var get = new HttpRequestMessage(HttpMethod.Get, url);
            using var fallback = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
            if (fallback.IsSuccessStatusCode) return (false, null);
            var message = $"HTTP {(int)fallback.StatusCode}";
            // Treat only clear missing-resource responses as broken; other failures may be temporary,
            // access-controlled, or unsupported and therefore cannot be scored confidently.
            if (fallback.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return (true, message);
            logger.LogWarning("Link check inconclusive for {Url}: {Status}", url, fallback.StatusCode);
            return (null, message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Polly.Timeout.TimeoutRejectedException)
        {
            logger.LogWarning(ex, "Link check failed for {Url}", url);
            return (null, ex.Message);
        }
    }
}
