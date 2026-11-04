using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Threading.Channels;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace Kinetic;

/// <summary>Crawls a website for PDF documents and optionally evaluates pages for quality and accessibility.</summary>
public sealed class WebsiteCrawler(
    IHttpClientFactory httpFactory,
    IDbContextFactory<DiscoveryDbContext> dbFactory,
    IOptions<CrawlerOptions> options,
    AccessibilityScanner accessibilityScanner,
    PageQualityScanner qualityScanner,
    ILogger<WebsiteCrawler> logger)
{
    private readonly CrawlerOptions _options = options.Value;

    /// <summary>Discovers pages and PDFs for a saved run and persists crawl and scan results.</summary>
    /// <param name="runId">The identifier of the discovery run to execute.</param>
    /// <param name="ct">A token that cancels page crawling and its background checks.</param>
    public async Task CrawlAsync(Guid runId, CancellationToken ct)
    {
        using var workersCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ct = workersCancellation.Token;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var run = await db.DiscoveryRuns.SingleAsync(x => x.Id == runId, ct);
        var root = ValidateRoot(run.RootUrl);
        var maxDepth = run.MaxDepth;
        var maxPages = run.MaxPages;
        if (run.RenderJavaScript && run.JavaScriptWaitSeconds is < 0 or > 60)
            throw new InvalidOperationException("JavaScript wait must be between 0 and 60 seconds.");
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
        run.PagesDiscovered = visited.Count;
        var linkJobs = Channel.CreateUnbounded<(Guid Id, Uri BaseUrl, string[] Links)>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var pdfJobs = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var linkWorker = RunWorkerAsync(() => CheckLinksAsync(linkJobs.Reader, client, ct), workersCancellation);
        var pdfWorker = RunWorkerAsync(() => ValidatePdfsAsync(pdfJobs.Reader, client, ct), workersCancellation);
        var crawlFinished = false;

        try
        {
            var sitemapsProcessed = !_options.CheckSitemaps;
            while ((queue.Count > 0 || !sitemapsProcessed) && !ct.IsCancellationRequested)
            {
                if (queue.Count == 0 && !sitemapsProcessed)
                {
                    await SetActivityAsync(db, run, "Discovering sitemaps", ct);
                    await DiscoverSitemapsAsync(db, run, root, client, queue, visited, pdfs, pdfJobs.Writer, allowedHosts, ct);
                    sitemapsProcessed = true;
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                var (url, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;
                try
                {
                    await Task.Delay(_options.DelayMilliseconds, ct);
                    await SetActivityAsync(db, run,
                        $"{(run.RenderJavaScript ? scanAccessibility ? "Rendering and checking accessibility" : "Rendering page" : "Loading page")}: {url}", ct);
                    string html;
                    var documentUrl = url;
                    PageAccessibilityResult? renderedAccessibility = null;
                    if (run.RenderJavaScript)
                    {
                        var rendered = await accessibilityScanner.RenderAsync(run.Id, url, run.JavaScriptWaitSeconds,
                            scanAccessibility && !scannedPages.Contains(Normalize(url)), ct);
                        html = rendered.Html;
                        documentUrl = rendered.FinalUrl;
                        renderedAccessibility = rendered.Accessibility;
                    }
                    else
                    {
                        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                        if (!response.IsSuccessStatusCode)
                        {
                            var protection = response.Headers.Server.Any(x =>
                                string.Equals(x.Product?.Name, "cloudflare", StringComparison.OrdinalIgnoreCase))
                                ? " Cloudflare may be blocking automated requests." : "";
                            throw new HttpRequestException(
                                $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) while loading {url}.{protection}",
                                null, response.StatusCode);
                        }
                        var media = response.Content.Headers.ContentType?.MediaType;
                        if (media is not null && !media.Contains("html", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"Expected an HTML page at {url}, but received {media}.");
                        html = await response.Content.ReadAsStringAsync(ct);
                    }
                    var parser = new HtmlParser();
                    var document = await parser.ParseDocumentAsync(html, ct);
                    if (scannedPages.Add(Normalize(url)))
                    {
                        var quality = qualityScanner.CreateResult(run.Id, documentUrl, document, scanAccessibility);
                        quality.Url = Normalize(url);
                        db.PageQualityResults.Add(quality);
                        await db.SaveChangesAsync(ct);
                        if (scanAccessibility)
                        {
                            await SetActivityAsync(db, run, $"Checking accessibility: {url}", ct);
                            var accessibility = renderedAccessibility is not null
                                ? StoreAccessibility(db, run, url, renderedAccessibility)
                                : await ScanAccessibilityAsync(db, run, url, ct);
                            quality.AccessibilityScore = accessibility.Score;
                            quality.BestPracticesScore = accessibility.BestPracticesScore;
                            var issues = System.Text.Json.JsonSerializer.Deserialize<List<QualityIssue>>(quality.IssuesJson)
                                ?? throw new InvalidOperationException("Invalid quality issues.");
                            issues.AddRange(accessibility.QualityIssues);
                            quality.IssuesJson = System.Text.Json.JsonSerializer.Serialize(issues);
                            await db.SaveChangesAsync(ct);
                        }
                        await linkJobs.Writer.WriteAsync((quality.Id, PageQualityScanner.GetLinkBase(documentUrl, document),
                            document.QuerySelectorAll("a[href], area[href]").Select(x => x.GetAttribute("href")!)
                                .Distinct(StringComparer.Ordinal).ToArray()), ct);
                        db.Entry(quality).State = EntityState.Detached;
                    }
                    run.PagesCrawled++;
                    var baseUrl = documentUrl;
                    if (run.RenderJavaScript &&
                        Uri.TryCreate(documentUrl, document.QuerySelector("base[href]")?.GetAttribute("href"), out var htmlBase) &&
                        htmlBase.Scheme is "http" or "https") baseUrl = htmlBase;
                    foreach (var candidate in ExtractResourceUrls(document))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!Uri.TryCreate(baseUrl, candidate.Url, out var target)) continue;
                        target = StripFragment(target);
                        if (target.Scheme is not ("http" or "https")) continue;
                        if (LooksLikePdf(target))
                        {
                            await DiscoverPdfAsync(db, run, target, url, pdfs, pdfJobs.Writer, ct);
                        }
                        else if (candidate.CanCrawl && LooksLikePage(target) &&
                                 depth < maxDepth && allowedHosts.Contains(target.Host))
                        {
                            var normalized = Normalize(target);
                            if (visited.Count < maxPages && visited.Add(normalized))
                            {
                                queue.Enqueue((target, depth + 1));
                                run.PagesDiscovered++;
                            }
                        }
                    }
                    await db.SaveChangesAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    logger.LogInformation("Excluded authentication-required page from scanning and scoring: {Url}", url);
                    // A browser-only 401 can arrive after the server-HTML quality result was saved.
                    var normalized = Normalize(url);
                    var quality = await db.PageQualityResults.SingleOrDefaultAsync(
                        x => x.DiscoveryRunId == run.Id && x.Url == normalized, ct);
                    if (quality is not null) db.PageQualityResults.Remove(quality);
                    if (depth == 0) run.Error = "Root page requires authentication (HTTP 401) and was excluded.";
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    run.FailedRequests++;
                    logger.LogWarning(ex, "Failed crawling {Url}", url);
                    if (depth == 0)
                        run.Error = $"Root page could not be scanned: {ex.Message.Split('\n')[0]}";
                    if (run.RenderJavaScript)
                    {
                        run.Error = $"JavaScript scan failed for {url}: {ex.Message.Split('\n')[0]}";
                        if (scanAccessibility && scannedPages.Add(Normalize(url)))
                            StoreAccessibility(db, run, url, new PageAccessibilityResult
                            {
                                DiscoveryRunId = run.Id, Url = Normalize(url), Error = run.Error
                            });
                        await db.SaveChangesAsync(ct);
                    }
                }
            }

            ct.ThrowIfCancellationRequested();
            crawlFinished = true;
            await SetActivityAsync(db, run, "Page discovery finished; waiting for link checks and PDF validation", ct);
        }
        finally
        {
            linkJobs.Writer.TryComplete();
            pdfJobs.Writer.TryComplete();
            if (!crawlFinished) await workersCancellation.CancelAsync();
            try
            {
                await Task.WhenAll(linkWorker, pdfWorker);
            }
            finally
            {
                // Workers have stopped before final writes, cancellation, deletion, or restart completes.
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }

        if (run.PagesCrawled == 0)
            throw new InvalidOperationException(
                $"No HTML pages could be scanned. {run.Error ?? "Check the root URL and the server's access restrictions."}");
    }

    private static async Task SetActivityAsync(DiscoveryDbContext db, DiscoveryRun run, string activity, CancellationToken ct)
    {
        run.CurrentActivity = activity;
        await db.SaveChangesAsync(ct);
    }

    private async Task DiscoverSitemapsAsync(DiscoveryDbContext db, DiscoveryRun run, Uri root, HttpClient client,
        Queue<(Uri Url, int Depth)> queue, HashSet<string> visited, HashSet<string> pdfs,
        ChannelWriter<Guid> pdfJobs, HashSet<string> allowed, CancellationToken ct)
    {
        var candidates = new[] { new Uri(root, "/robots.txt"), new Uri(root, "/sitemap.xml"), new Uri(root, "/sitemap_index.xml") };
        var sitemapUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await SetActivityAsync(db, run, $"Reading discovery file: {candidate}", ct);
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
                await SetActivityAsync(db, run, $"Reading sitemap: {uri}", ct);
                var xml = await client.GetStringAsync(uri, ct);
                var doc = XDocument.Parse(xml);
                foreach (var loc in doc.Descendants().Where(x => x.Name.LocalName == "loc").Select(x => x.Value.Trim()))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Uri.TryCreate(loc, UriKind.Absolute, out var item)) continue;
                    if (LooksLikePdf(item))
                    {
                        await DiscoverPdfAsync(db, run, item, uri, pdfs, pdfJobs, ct);
                    }
                    else if (depth < 3 && item.Host == root.Host &&
                             item.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        pending.Enqueue((item, depth + 1));
                    else if (LooksLikePage(item) && run.MaxDepth >= 1 && allowed.Contains(item.Host) && item.Host == root.Host &&
                             visited.Count < run.MaxPages && visited.Add(Normalize(item)))
                    {
                        queue.Enqueue((item, 1));
                        run.PagesDiscovered++;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Unable to process sitemap {Url}", uri);
            }
        }
    }

    private static async Task RunWorkerAsync(Func<Task> work, CancellationTokenSource cancellation)
    {
        try { await work(); }
        catch
        {
            await cancellation.CancelAsync();
            throw;
        }
    }

    private async Task CheckLinksAsync(ChannelReader<(Guid Id, Uri BaseUrl, string[] Links)> jobs,
        HttpClient client, CancellationToken ct)
    {
        await foreach (var job in jobs.ReadAllAsync(ct))
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var quality = await db.PageQualityResults.SingleAsync(x => x.Id == job.Id, ct);
            await qualityScanner.ScanLinksAsync(quality, job.BaseUrl, job.Links, client, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task ValidatePdfsAsync(ChannelReader<Guid> jobs, HttpClient client, CancellationToken ct)
    {
        await foreach (var id in jobs.ReadAllAsync(ct))
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var pdf = await db.DiscoveredPdfs.SingleAsync(x => x.Id == id, ct);
            await ValidatePdfAsync(pdf, client, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            if (pdf.Status == DiscoveredPdfStatus.Valid)
                await db.DiscoveryRuns.Where(x => x.Id == pdf.DiscoveryRunId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PdfsValidated, x => x.PdfsValidated + 1), ct);
            await transaction.CommitAsync(ct);
        }
    }

    private static async Task DiscoverPdfAsync(DiscoveryDbContext db, DiscoveryRun run, Uri pdf, Uri source,
        HashSet<string> seen, ChannelWriter<Guid> jobs, CancellationToken ct)
    {
        var key = Normalize(pdf);
        if (!seen.Add(key)) return;
        var record = new DiscoveredPdf
        {
            DiscoveryRunId = run.Id, Url = key, SourcePageUrl = source.AbsoluteUri,
            FileName = Path.GetFileName(pdf.LocalPath), Status = DiscoveredPdfStatus.Pending,
            DiscoveryMethod = "SitemapOrHtml"
        };
        db.DiscoveredPdfs.Add(record);
        run.PdfsDiscovered++;
        await db.SaveChangesAsync(ct);
        db.Entry(record).State = EntityState.Detached;
        await jobs.WriteAsync(record.Id, ct);
    }

    private async Task ValidatePdfAsync(DiscoveredPdf record, HttpClient client, CancellationToken ct)
    {
        var pdf = new Uri(record.Url);
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
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var bytes = new byte[1024];
                    var read = await stream.ReadAtLeastAsync(bytes, 5, throwOnEndOfStream: false, cancellationToken: ct);
                    status = read >= 5 && HasPdfSignature(bytes) || contentType == "application/pdf"
                        ? DiscoveredPdfStatus.Valid : DiscoveredPdfStatus.Unverified;
                }
            }
            else status = DiscoveredPdfStatus.Valid;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or
                                   Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            logger.LogWarning(ex, "PDF validation failed for {Url}", pdf);
            message = ex.Message;
            status = DiscoveredPdfStatus.Unverified;
        }

        record.ContentType = contentType;
        record.FileSizeBytes = size;
        record.Status = status;
        record.LastVerifiedAt = DateTimeOffset.UtcNow;
        record.ValidationMessage = message;
    }

    private async Task<PageAccessibilityResult> ScanAccessibilityAsync(DiscoveryDbContext db, DiscoveryRun run, Uri url, CancellationToken ct)
    {
        var result = await accessibilityScanner.ScanAsync(run.Id, url, ct);
        return StoreAccessibility(db, run, url, result);
    }

    private static PageAccessibilityResult StoreAccessibility(DiscoveryDbContext db, DiscoveryRun run, Uri url,
        PageAccessibilityResult result)
    {
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

    private static IEnumerable<(string Url, bool CanCrawl)> ExtractResourceUrls(AngleSharp.Dom.IDocument doc)
    {
        foreach (var element in doc.QuerySelectorAll("a[href], area[href], iframe[src], frame[src], embed[src], object[data], source[src], link[href]"))
        {
            var value = element.GetAttribute("href") ?? element.GetAttribute("src") ?? element.GetAttribute("data");
            if (!string.IsNullOrWhiteSpace(value))
                yield return (value, element.LocalName is "a" or "area" or "iframe" or "frame");
        }
        foreach (var element in doc.QuerySelectorAll("[data-href], [data-url], [data-download-url]"))
            foreach (var attr in new[] { "data-href", "data-url", "data-download-url" })
                if (element.HasAttribute(attr)) yield return (element.GetAttribute(attr)!, true);
    }

    private static bool LooksLikePage(Uri uri) =>
        Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() is not (
            ".css" or ".js" or ".mjs" or ".map" or ".json" or ".xml" or ".txt" or
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".svg" or ".webp" or ".ico" or ".avif" or
            ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot" or
            ".mp3" or ".mp4" or ".webm" or ".wav" or ".ogg" or ".mov" or
            ".zip" or ".gz" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx");

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
