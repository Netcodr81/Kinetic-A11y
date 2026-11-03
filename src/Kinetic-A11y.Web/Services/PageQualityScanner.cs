using AngleSharp.Dom;
using System.Net;
using System.Text.Json;

namespace Kinetic;

public sealed class PageQualityScanner(ILogger<PageQualityScanner> logger)
{
    private readonly Dictionary<string, (bool? Broken, string? Error)> _linkChecks = new(StringComparer.Ordinal);

    public async Task<PageQualityResult> ScanAsync(Guid runId, Uri url, IDocument document,
        HttpClient client, bool accessibilityEnabled, CancellationToken ct)
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
        result.SeoScore = AccessibilityScoring.Calculate(issues.Where(x => x.Category == "SEO").Select(x => x.RuleId));

        var baseUri = url;
        var baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (Uri.TryCreate(url, baseHref, out var resolvedBase) && resolvedBase.Scheme is "http" or "https")
            baseUri = resolvedBase;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var link in document.QuerySelectorAll("a[href], area[href]"))
        {
            ct.ThrowIfCancellationRequested();
            var href = link.GetAttribute("href")!.Trim();
            if (href.Length == 0 || href.StartsWith('#')) continue;
            if (!Uri.TryCreate(baseUri, href, out var target))
            {
                issues.Add(new("Broken Links", $"invalid:{href}", "Invalid link URL.", href));
                continue;
            }
            if (target.Scheme is not ("http" or "https")) continue;
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
        result.BrokenLinksScore = errors.Count == 0
            ? AccessibilityScoring.Calculate(issues.Where(x => x.Category == "Broken Links").Select(x => x.RuleId))
            : null;
        result.IssuesJson = JsonSerializer.Serialize(issues.DistinctBy(x => (x.Category, x.RuleId)));
        return result;
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
            if (fallback.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return (true, message);
            logger.LogWarning("Link check inconclusive for {Url}: {Status}", url, fallback.StatusCode);
            return (null, message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Link check failed for {Url}", url);
            return (null, ex.Message);
        }
    }
}
