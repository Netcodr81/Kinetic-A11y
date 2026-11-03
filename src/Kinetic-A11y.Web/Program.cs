using Kinetic_A11y.Web.Components;
using Kinetic;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<DiscoveryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Discovery")
        ?? "Data Source=pdf-discovery.db"));

builder.Services.AddHttpClient("crawler", client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("StatePdfDiscoveryMvp/1.0 (+contact administrator)");
}).AddStandardResilienceHandler();

builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection("Crawler"));
builder.Services.Configure<AccessibilityOptions>(builder.Configuration.GetSection("Accessibility"));
builder.Services.AddSingleton<AccessibilityScanner>();
builder.Services.AddSingleton<DiscoveryQueue>();
builder.Services.AddSingleton<DiscoveryRunControl>();
builder.Services.AddScoped<WebsiteCrawler>();
builder.Services.AddScoped<PageQualityScanner>();
builder.Services.AddHostedService<DiscoveryWorker>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiscoveryDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    try
    {
        // EnsureCreated does not upgrade existing databases; probe for the newest schema elements.
        _ = await db.DiscoveryRuns.AsNoTracking().Where(x => x.ScanAccessibility && x.AverageAccessibilityScore != null).AnyAsync();
        _ = await db.AccessibilityViolations.AsNoTracking().AnyAsync();
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex)
    {
        app.Logger.LogWarning(ex, "Discovery database schema is out of date; recreating it. Existing runs will be lost.");
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
    await db.EnsureQualitySchemaAsync();
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAntiforgery();
app.MapGet("/runs/{runId:guid}/export", async (Guid runId, IDbContextFactory<DiscoveryDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var rows = await db.DiscoveredPdfs.AsNoTracking().Where(x => x.DiscoveryRunId == runId)
        .OrderBy(x => x.FileName).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    var lines = new List<string> { "FileName,Url,SourcePageUrl,ContentType,FileSizeBytes,Status,DiscoveryMethod" };
    lines.AddRange(rows.Select(x => string.Join(",", Csv(x.FileName), Csv(x.Url), Csv(x.SourcePageUrl),
        Csv(x.ContentType), x.FileSizeBytes?.ToString() ?? "", Csv(x.Status.ToString()), Csv(x.DiscoveryMethod))));
    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv", $"discovered-pdfs-{runId}.csv");
});
app.MapGet("/runs/{runId:guid}/accessibility/export", async (Guid runId, IDbContextFactory<DiscoveryDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var pages = await db.PageAccessibilityResults.AsNoTracking().Include(x => x.Violations)
        .Where(x => x.DiscoveryRunId == runId).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    var quality = await db.PageQualityResults.AsNoTracking().Where(x => x.DiscoveryRunId == runId)
        .ToDictionaryAsync(x => x.Url, ct);
    var lines = new List<string> { "PageUrl,Score,ScoringMethod,Critical,Serious,Moderate,Minor,PageError,RuleId,Impact,NodeCount,Help,HelpUrl,SampleTarget" };
    foreach (var page in pages.OrderBy(x => x.Score ?? -1).ThenBy(x => x.Url))
    {
        var prefix = string.Join(",", Csv(page.Url), page.Score?.ToString() ?? "",
            Csv(quality.ContainsKey(page.Url) ? "DubBot-style" : "Legacy weighted"), page.CriticalCount, page.SeriousCount,
            page.ModerateCount, page.MinorCount, Csv(page.Error));
        if (page.Violations.Count == 0) { lines.Add(prefix + ",,,,,,"); continue; }
        lines.AddRange(page.Violations
            .OrderByDescending(v => AccessibilityScoring.ImpactWeight(v.Impact)).ThenByDescending(v => v.NodeCount)
            .Select(v => string.Join(",", prefix, Csv(v.RuleId), Csv(v.Impact), v.NodeCount, Csv(v.Help), Csv(v.HelpUrl), Csv(v.SampleTarget))));
    }
    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv", $"accessibility-{runId}.csv");
});
app.MapGet("/runs/{runId:guid}/quality/export", async (Guid runId, IDbContextFactory<DiscoveryDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var pages = await db.PageQualityResults.AsNoTracking().Where(x => x.DiscoveryRunId == runId).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    static string Number(double? value) => value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    var lines = new List<string> { "PageUrl,OverallScore,AccessibilityScore,BrokenLinksScore,SeoScore,BestPracticesScore,LinkCheckError,IssuesJson" };
    lines.AddRange(pages.OrderBy(x => x.Url).Select(x => string.Join(",", Csv(x.Url), Number(x.OverallScore),
        Number(x.AccessibilityScore), Number(x.BrokenLinksScore), Number(x.SeoScore), Number(x.BestPracticesScore),
        Csv(x.LinkCheckError), Csv(x.IssuesJson))));
    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv", $"quality-{runId}.csv");
});
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
