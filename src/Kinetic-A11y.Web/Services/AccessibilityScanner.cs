using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;


namespace Kinetic;

public sealed class AccessibilityScanner(
    IOptions<AccessibilityOptions> options,
    ILogger<AccessibilityScanner> logger) : IAsyncDisposable
{
    private const int MaxSnippetLength = 1000;
    private readonly AccessibilityOptions _options = options.Value;
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _installAttempted;

    public bool IsEnabled => _options.Enabled;

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

            var axe = await page.RunAxe(new AxeRunOptions
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
            ct.ThrowIfCancellationRequested();
            Map(axe, result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogDebug(ex, "Accessibility scan failed for {Url}", url);
            result.Error = Truncate(ex.Message.Split('\n')[0], 2000);
        }
        return result;
    }

    private void Map(AxeResult axe, PageAccessibilityResult result)
    {
        var allViolations = axe.Violations ?? [];
        var violations = allViolations.Where(v => v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal))).ToArray();
        var bestPractices = allViolations.Where(v => !v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal))).ToArray();
        result.ViolationCount = violations.Select(v => v.Id).Distinct().Count();
        result.PassCount = axe.Passes?.Length ?? 0;
        result.IncompleteCount = axe.Incomplete?.Length ?? 0;
        result.CriticalCount = violations.Count(v => Is(v.Impact, "critical"));
        result.SeriousCount = violations.Count(v => Is(v.Impact, "serious"));
        result.ModerateCount = violations.Count(v => Is(v.Impact, "moderate"));
        result.MinorCount = violations.Count(v => Is(v.Impact, "minor"));
        result.Score = AccessibilityScoring.Calculate(violations.Select(v => v.Id));
        result.BestPracticesScore = AccessibilityScoring.Calculate(bestPractices.Select(v => v.Id));
        result.QualityIssues.AddRange(allViolations.Select(v => new QualityIssue(
            v.Tags.Any(t => t.StartsWith("wcag", StringComparison.Ordinal)) ? "Accessibility" : "Best Practices",
            v.Id, v.Help, v.Nodes?.FirstOrDefault()?.Target?.ToString())));

        foreach (var v in violations
                     .OrderByDescending(v => AccessibilityScoring.ImpactWeight(v.Impact))
                     .ThenByDescending(v => v.Nodes?.Length ?? 0)
                     .Take(Math.Max(0, _options.MaxViolationsStoredPerPage)))
        {
            var node = v.Nodes?.FirstOrDefault();
            result.Violations.Add(new AccessibilityViolation
            {
                RuleId = Truncate(v.Id, 256) ?? "unknown",
                Impact = v.Impact,
                Description = v.Description,
                Help = v.Help,
                HelpUrl = Truncate(v.HelpUrl, 2048),
                NodeCount = v.Nodes?.Length ?? 0,
                SampleTarget = Truncate(node?.Target?.ToString(), MaxSnippetLength),
                SampleHtml = Truncate(node?.Html, MaxSnippetLength)
            });
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

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _browserLock.Dispose();
    }
}
