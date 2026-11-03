using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace Kinetic;

public sealed class WebsiteCrawler(
    IHttpClientFactory httpFactory,
    IDbContextFactory<DiscoveryDbContext> dbFactory,
    IOptions<CrawlerOptions> options,
    AccessibilityScanner accessibilityScanner,
    PageQualityScanner qualityScanner,
    ILogger<WebsiteCrawler> logger)
{
    private readonly CrawlerOptions _options = options.Value;

    public async Task CrawlAsync(Guid runId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var run = await db.DiscoveryRuns.SingleAsync(x => x.Id == runId, ct);
        var root = ValidateRoot(run.RootUrl);
        var maxDepth = run.MaxDepth;
        var maxPages = run.MaxPages;
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Host };
        foreach (var host in _options.AllowedHosts.Where(x => !string.IsNullOrWhiteSpace(x)))
            allowedHosts.Add(host.Trim());

        var client = httpFactory.CreateClient("crawler");
        var queue = new Queue<(Uri Url, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pdfs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scannedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanAccessibility = run.ScanAccessibility && accessibilityScanner.IsEnabled;
        queue.Enqueue((root, 0));
        visited.Add(Normalize(root));

        try
        {
            if (_options.CheckSitemaps)
                await DiscoverSitemapsAsync(run, root, client, queue, pdfs, allowedHosts, ct);

            while (queue.Count > 0 && visited.Count <= maxPages && !ct.IsCancellationRequested)
            {
                var (url, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;
                try
                {
                    await Task.Delay(_options.DelayMilliseconds, ct);
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!response.IsSuccessStatusCode) { run.FailedRequests++; continue; }
                    var media = response.Content.Headers.ContentType?.MediaType;
                    if (media is not null && !media.Contains("html", StringComparison.OrdinalIgnoreCase)) continue;
                    var html = await response.Content.ReadAsStringAsync(ct);
                    run.PagesCrawled++;

                    var parser = new HtmlParser();
                    var document = await parser.ParseDocumentAsync(html, ct);
                    foreach (var candidate in ExtractResourceUrls(document))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!Uri.TryCreate(url, candidate, out var target)) continue;
                        target = StripFragment(target);
                        if (target.Scheme is not ("http" or "https")) continue;
                        if (LooksLikePdf(target))
                            await ValidateAndStoreAsync(run, target, url, client, pdfs, ct);
                        else if (depth < maxDepth && allowedHosts.Contains(target.Host))
                        {
                            var normalized = Normalize(target);
                            if (visited.Add(normalized) && visited.Count <= maxPages)
                            {
                                queue.Enqueue((target, depth + 1));
                                run.PagesDiscovered++;
                            }
                        }
                    }
                    if (scannedPages.Add(Normalize(url)))
                    {
                        var quality = await qualityScanner.ScanAsync(run.Id, url, document, client,
                            scanAccessibility, ct);
                        quality.Url = Normalize(url);
                        db.PageQualityResults.Add(quality);
                        if (scanAccessibility)
                        {
                            var accessibility = await ScanAccessibilityAsync(db, run, url, ct);
                            quality.AccessibilityScore = accessibility.Score;
                            quality.BestPracticesScore = accessibility.BestPracticesScore;
                            var issues = System.Text.Json.JsonSerializer.Deserialize<List<QualityIssue>>(quality.IssuesJson)
                                ?? throw new InvalidOperationException("Invalid quality issues.");
                            issues.AddRange(accessibility.QualityIssues);
                            quality.IssuesJson = System.Text.Json.JsonSerializer.Serialize(issues);
                        }
                    }
                    await db.SaveChangesAsync(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    run.FailedRequests++;
                    logger.LogDebug(ex, "Failed crawling {Url}", url);
                }
            }

            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            // Preserve partial progress even when the run's cancellation token is cancelled.
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task DiscoverSitemapsAsync(DiscoveryRun run, Uri root, HttpClient client,
        Queue<(Uri Url, int Depth)> queue, HashSet<string> pdfs, HashSet<string> allowed, CancellationToken ct)
    {
        var candidates = new[] { new Uri(root, "/robots.txt"), new Uri(root, "/sitemap.xml"), new Uri(root, "/sitemap_index.xml") };
        var sitemapUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = await client.GetStringAsync(candidate, ct);
                if (candidate.AbsolutePath.EndsWith("robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var line in text.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
                            sitemapUrls.Add(trimmed["Sitemap:".Length..].Trim());
                    }
                }
                else sitemapUrls.Add(candidate.AbsoluteUri);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Optional discovery file unavailable: {Url}", candidate);
            }
        }

        var pending = new Queue<(Uri Url, int Depth)>();
        foreach (var value in sitemapUrls)
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri)) pending.Enqueue((uri, 0));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0 && seen.Count < 100)
        {
            ct.ThrowIfCancellationRequested();
            var (uri, depth) = pending.Dequeue();
            if (!seen.Add(uri.AbsoluteUri)) continue;
            try
            {
                var xml = await client.GetStringAsync(uri, ct);
                var doc = XDocument.Parse(xml);
                foreach (var loc in doc.Descendants().Where(x => x.Name.LocalName == "loc").Select(x => x.Value.Trim()))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Uri.TryCreate(loc, UriKind.Absolute, out var item)) continue;
                    if (LooksLikePdf(item))
                    {
                        await ValidateAndStoreAsync(run, item, uri, client, pdfs, ct);
                    }
                    else if (depth < 3 && item.Host == root.Host &&
                             item.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        pending.Enqueue((item, depth + 1));
                    else if (allowed.Contains(item.Host) && item.Host == root.Host)
                        queue.Enqueue((item, 1));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Unable to process sitemap {Url}", uri);
            }
        }
    }

    private async Task ValidateAndStoreAsync(DiscoveryRun? run, Uri pdf, Uri source,
        HttpClient client, HashSet<string> seen, CancellationToken ct)
    {
        var key = Normalize(pdf);
        if (!seen.Add(key)) return;
        var status = DiscoveredPdfStatus.Unverified;
        string? contentType = null;
        long? size = null;
        string? message = null;
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, pdf);
            using var headResponse = await client.SendAsync(head, ct);
            contentType = headResponse.Content.Headers.ContentType?.MediaType;
            size = headResponse.Content.Headers.ContentLength;
            if (!headResponse.IsSuccessStatusCode || contentType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) != true)
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, pdf);
                get.Headers.Range = new RangeHeaderValue(0, 1023);
                using var response = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                contentType = response.Content.Headers.ContentType?.MediaType ?? contentType;
                size ??= response.Content.Headers.ContentLength;
                if (!response.IsSuccessStatusCode) { status = DiscoveredPdfStatus.Unavailable; message = $"HTTP {(int)response.StatusCode}"; }
                else
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                    status = HasPdfSignature(bytes) || contentType == "application/pdf"
                        ? DiscoveredPdfStatus.Valid : DiscoveredPdfStatus.Unverified;
                }
            }
            else status = DiscoveredPdfStatus.Valid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            message = ex.Message;
            status = DiscoveredPdfStatus.Unverified;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var runId = run!.Id;
        var exists = await db.DiscoveredPdfs.AnyAsync(x => x.DiscoveryRunId == runId && x.Url == key, ct);
        if (exists) return;
        db.DiscoveredPdfs.Add(new DiscoveredPdf
        {
            DiscoveryRunId = runId,
            Url = key,
            SourcePageUrl = source.AbsoluteUri,
            FileName = Path.GetFileName(pdf.LocalPath),
            ContentType = contentType,
            FileSizeBytes = size,
            Status = status,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            ValidationMessage = message,
            DiscoveryMethod = "SitemapOrHtml"
        });
        await db.SaveChangesAsync(ct);
        if (run is not null)
        {
            run.PdfsDiscovered++;
            if (status == DiscoveredPdfStatus.Valid) run.PdfsValidated++;
        }
    }

    private async Task<PageAccessibilityResult> ScanAccessibilityAsync(DiscoveryDbContext db, DiscoveryRun run, Uri url, CancellationToken ct)
    {
        var result = await accessibilityScanner.ScanAsync(run.Id, url, ct);
        result.Url = Normalize(url);
        db.PageAccessibilityResults.Add(result);
        if (result.Score is { } score)
        {
            var scored = run.PagesAccessibilityScanned;
            run.AverageAccessibilityScore = ((run.AverageAccessibilityScore ?? 0) * scored + score) / (scored + 1);
            run.PagesAccessibilityScanned++;
        }
        else run.AccessibilityScanFailures++;
        return result;
    }

    private static IEnumerable<string> ExtractResourceUrls(AngleSharp.Dom.IDocument doc)
    {
        foreach (var element in doc.QuerySelectorAll("a[href], area[href], iframe[src], frame[src], embed[src], object[data], source[src], link[href]"))
        {
            var value = element.GetAttribute("href") ?? element.GetAttribute("src") ?? element.GetAttribute("data");
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
        foreach (var element in doc.QuerySelectorAll("[data-href], [data-url], [data-download-url]"))
            foreach (var attr in new[] { "data-href", "data-url", "data-download-url" })
                if (element.HasAttribute(attr)) yield return element.GetAttribute(attr)!;
    }

    private static bool LooksLikePdf(Uri uri) =>
        uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
        uri.Query.Contains("format=pdf", StringComparison.OrdinalIgnoreCase) ||
        uri.Query.Contains("type=pdf", StringComparison.OrdinalIgnoreCase);

    private static bool HasPdfSignature(byte[] bytes) =>
        bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;

    private static Uri ValidateRoot(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Enter a valid public HTTP or HTTPS root URL.");
        return uri;
    }

    private static Uri StripFragment(Uri uri)
    {
        var builder = new UriBuilder(uri) { Fragment = "" };
        return builder.Uri;
    }

    private static string Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri) { Fragment = "" };
        if (builder.Path.Length > 1) builder.Path = builder.Path.TrimEnd('/');
        return builder.Uri.AbsoluteUri;
    }
}
