using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;


namespace Kinetic;

/// <summary>Loads pages in Chromium and maps axe-core findings to discovery result models.</summary>
public sealed class AccessibilityScanner(
    IOptions<AccessibilityOptions> options,
    IssuePreviewStore previews,
    ILogger<AccessibilityScanner> logger) : IAsyncDisposable
{
    private const int MaxSnippetLength = 1000;
    private readonly AccessibilityOptions _options = options.Value;
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _installAttempted;

    /// <summary>Gets whether accessibility checks are globally enabled by configuration.</summary>
    public bool IsEnabled => _options.Enabled;

    /// <summary>Renders a page and optionally checks its accessibility in the same browser page.</summary>
    /// <param name="runId">The discovery run that owns the result.</param>
    /// <param name="url">The page URL to load.</param>
    /// <param name="waitSeconds">Additional time to wait after DOM content loads, from 0 to 60 seconds.</param>
    /// <param name="scanAccessibility">Whether to run axe-core checks when the global setting is enabled.</param>
    /// <param name="ct">A token that cancels navigation, waiting, and scanning.</param>
    /// <returns>The rendered HTML, final browser URL, and an optional accessibility result.</returns>
    public async Task<(string Html, Uri FinalUrl, PageAccessibilityResult? Accessibility)> RenderAsync(
        Guid runId, Uri url, int waitSeconds, bool scanAccessibility, CancellationToken ct)
    {
        if (waitSeconds is < 0 or > 60)
            throw new InvalidOperationException("JavaScript wait must be between 0 and 60 seconds.");
        var browser = await GetBrowserAsync(ct);
        ct.ThrowIfCancellationRequested();
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "StatePdfDiscoveryMvp/1.0 (+contact administrator) axe-core"
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.PageTimeoutSeconds * 1000f);
        await using var registration = ct.Register(() => _ = page.CloseAsync());
        try
        {
            ct.ThrowIfCancellationRequested();
            var response = await page.GotoAsync(url.AbsoluteUri, new()
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _options.PageTimeoutSeconds * 1000f
            });
            ct.ThrowIfCancellationRequested();
            if (response is not null && !response.Ok)
                throw new InvalidOperationException($"HTTP {response.Status} while rendering {url}.");
            var contentType = response is null ? null : await response.HeaderValueAsync("content-type");
            if (contentType is not null && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cannot render non-HTML content at {url}.");
            await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
            var html = await page.ContentAsync();
            var finalUrl = new Uri(page.Url);
            PageAccessibilityResult? result = null;
            if (scanAccessibility && IsEnabled)
            {
                result = new PageAccessibilityResult { DiscoveryRunId = runId, Url = url.AbsoluteUri };
                try { await MapAsync(await RunChecksAsync(page), result, page, ct); }
                catch (Exception ex)
                {
                    ct.ThrowIfCancellationRequested();
                    logger.LogWarning(ex, "Accessibility check failed after rendering {Url}", url);
                    result.Error = Truncate(ex.Message.Split('\n')[0], 2000);
                }
            }
            ct.ThrowIfCancellationRequested();
            return (html, finalUrl, result);
        }
        catch (Exception)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
    }

    /// <summary>Loads a page in Chromium and records its axe-core accessibility findings.</summary>
    /// <param name="runId">The discovery run that owns the result.</param>
    /// <param name="url">The page URL to scan.</param>
    /// <param name="ct">A token that cancels navigation and scanning.</param>
    /// <returns>The page result, including an error instead of a score if the scan fails.</returns>
    public async Task<PageAccessibilityResult> ScanAsync(Guid runId, Uri url, CancellationToken ct)
    {
        var result = new PageAccessibilityResult { DiscoveryRunId = runId, Url = url.AbsoluteUri };
        try
        {
            var browser = await GetBrowserAsync(ct);
            ct.ThrowIfCancellationRequested();
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "StatePdfDiscoveryMvp/1.0 (+contact administrator) axe-core"
            });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(_options.PageTimeoutSeconds * 1000f);
            await using var registration = ct.Register(() => _ = page.CloseAsync());
            ct.ThrowIfCancellationRequested();

            var response = await page.GotoAsync(url.AbsoluteUri, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = _options.PageTimeoutSeconds * 1000f
            });
            ct.ThrowIfCancellationRequested();
            if (response is not null && !response.Ok)
            {
                result.Error = $"HTTP {response.Status} when loading page in browser";
                return result;
            }

            var axe = await RunChecksAsync(page);
            ct.ThrowIfCancellationRequested();
            await MapAsync(axe, result, page, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(ex, "Accessibility scan failed for {Url}", url);
            result.Error = Truncate(ex.Message.Split('\n')[0], 2000);
        }
        return result;
    }

    // Run WCAG rules configured for this app plus axe-core best practices. Incomplete results
    // are retained separately from violations because they need manual review, not a confirmed-failure score.
    private Task<AxeResult> RunChecksAsync(IPage page) => page.RunAxe(new AxeRunOptions
    {
        // WCAG A/AA and accessibility best practices
        RunOnly = RunOnlyOptions.Tags(_options.Tags.Append("best-practice").Distinct().ToArray()),

        // Include accessibility violations and potential issues
        ResultTypes = new HashSet<ResultType>
                {
                    ResultType.Violations,
                    ResultType.Incomplete
                },

        // Element identification
        Selectors = true,
        Ancestry = true,
        XPath = false,
        AbsolutePaths = false,

        // Scan behavior
        Iframes = true,
        RestoreScroll = true
    });

    private async Task MapAsync(AxeResult axe, PageAccessibilityResult result, IPage page, CancellationToken ct)
    {
        var allViolations = axe.Violations ?? [];
        // The configured WCAG tags identify accessibility findings; other selected rules are
        // reported as best practices so they can be shown separately from the accessibility score.
        var violations = allViolations.Where(v => v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal))).ToArray();
        var bestPractices = allViolations.Where(v => !v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal))).ToArray();

        // Counts and scores are per failed rule, not per affected element. Impact counts are
        // also rule counts; impact severity affects the display order below, not the score.
        result.ViolationCount = violations.Select(v => v.Id).Distinct().Count();
        result.PassCount = axe.Passes?.Length ?? 0;
        result.IncompleteCount = axe.Incomplete?.Length ?? 0;
        result.CriticalCount = violations.Count(v => Is(v.Impact, "critical"));
        result.SeriousCount = violations.Count(v => Is(v.Impact, "serious"));
        result.ModerateCount = violations.Count(v => Is(v.Impact, "moderate"));
        result.MinorCount = violations.Count(v => Is(v.Impact, "minor"));
        result.Score = AccessibilityScoring.Calculate(violations.Select(v => v.Id));
        result.BestPracticesScore = AccessibilityScoring.Calculate(bestPractices.Select(v => v.Id));

        // Keep every issue in the quality summary, but persist only a capped set of representative
        // WCAG violations; the highest-impact rules with the most affected nodes are stored first.
        result.QualityIssues.AddRange(allViolations.Select(v => new QualityIssue(
            v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal)) ? "Accessibility" : "Best Practices",
            v.Id, v.Help, v.Nodes?.FirstOrDefault()?.Target?.ToString())));

        var previewCount = 0;
        foreach (var v in violations
                     .OrderByDescending(v => AccessibilityScoring.ImpactWeight(v.Impact))
                     .ThenByDescending(v => v.Nodes?.Length ?? 0)
                     .Take(Math.Max(0, _options.MaxViolationsStoredPerPage)))
        {
            var node = v.Nodes?.FirstOrDefault();
            var violation = new AccessibilityViolation
            {
                RuleId = Truncate(v.Id, 256) ?? "unknown",
                Impact = v.Impact,
                Description = v.Description,
                Help = v.Help,
                HelpUrl = Truncate(v.HelpUrl, 2048),
                NodeCount = v.Nodes?.Length ?? 0,
                SampleTarget = Truncate(FormatTarget(node?.Target), MaxSnippetLength),
                SampleHtml = Truncate(node?.Html, MaxSnippetLength)
            };
            result.Violations.Add(violation);

            if (_options.CaptureIssuePreviews && previewCount < Math.Max(0, _options.MaxIssuePreviewsPerPage))
            {
                var selector = GetTargetSelector(node?.Target);
                if (!string.IsNullOrWhiteSpace(selector))
                {
                    try
                    {
                        var image = await CaptureHighlightedPreviewAsync(page, selector, ct);
                        if (image is not null)
                        {
                            await previews.SaveAsync(result.DiscoveryRunId, violation.Id, image, ct);
                            violation.PreviewPath = previews.GetRelativePath(result.DiscoveryRunId, violation.Id);
                            previewCount++;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not capture preview for accessibility rule {RuleId} on {Url}",
                            violation.RuleId, result.Url);
                    }
                }
            }
        }
    }

    private static string? FormatTarget(object? target) => target switch
    {
        string selector => selector,
        IEnumerable<string> selectors => string.Join(" > ", selectors),
        _ => target?.ToString()
    };

    private static string? GetTargetSelector(object? target) => target switch
    {
        string selector => selector,
        IEnumerable<string> selectors => GetSingleSelector(selectors),
        _ => target?.ToString()
    };

    private static string? GetSingleSelector(IEnumerable<string> selectors)
    {
        using var enumerator = selectors.GetEnumerator();
        if (!enumerator.MoveNext()) return null;
        var selector = enumerator.Current;
        return enumerator.MoveNext() ? null : selector;
    }

    private async Task<byte[]?> CaptureHighlightedPreviewAsync(IPage page, string selector, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var element = page.Locator(selector).First;
        if (await element.CountAsync() == 0) return null;

        await element.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
        {
            Timeout = _options.PageTimeoutSeconds * 1000f
        });
        var originalStyle = await element.GetAttributeAsync("style");
        try
        {
            await element.EvaluateAsync("""
                element => {
                    element.style.outline = "4px solid #d00000";
                    element.style.outlineOffset = "3px";
                    element.scrollIntoView({ block: "center", inline: "center" });
                }
                """);
            return await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false,
                Animations = ScreenshotAnimations.Disabled
            });
        }
        finally
        {
            await element.EvaluateAsync("""
                (element, style) => {
                    if (style === null) element.removeAttribute("style");
                    else element.setAttribute("style", style);
                }
                """, originalStyle);
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is { IsConnected: true }) return _browser;
        await _browserLock.WaitAsync(ct);
        try
        {
            if (_browser is { IsConnected: true }) return _browser;
            _playwright ??= await Playwright.CreateAsync();
            try
            {
                _browser = await LaunchAsync();
            }
            catch (PlaywrightException ex) when (_options.AutoInstallBrowsers && !_installAttempted &&
                                                 ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
            {
                _installAttempted = true;
                logger.LogWarning("Playwright Chromium not found; installing it now (one-time download)...");
                var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]), ct);
                if (exitCode != 0) throw new InvalidOperationException($"Playwright browser install failed with exit code {exitCode}.", ex);
                _browser = await LaunchAsync();
            }
            return _browser;
        }
        finally { _browserLock.Release(); }
    }

    private Task<IBrowser> LaunchAsync() =>
        _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = _options.Headless });

    private static bool Is(string? impact, string expected) =>
        string.Equals(impact, expected, StringComparison.OrdinalIgnoreCase);

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    /// <summary>Releases the shared Chromium browser, Playwright instance, and synchronization resource.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _browserLock.Dispose();
    }
}
