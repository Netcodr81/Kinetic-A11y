using Kinetic_A11y.Web2.Components;
using Kinetic_A11y.Web2.Web;
using Kinetic_A11y.Web2.Web.Components;
using Kinetic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddDataProtection().SetApplicationName("CIOBlazorPublicTemplate")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90))
    .PersistKeysToFileSystem(new DirectoryInfo(Environment.CurrentDirectory + "\\App_Keys\\"));

builder.Services.AddWebServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddDiscoveryServices(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.Services.InitializeDiscoveryDatabaseAsync(app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseSerilogRequestLogging();

app.UseAntiforgery();

app.MapGet("/runs/{runId:guid}/export", async (
    Guid runId,
    IDbContextFactory<DiscoveryDbContext> factory,
    CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var rows = await db.DiscoveredPdfs.AsNoTracking().Where(x => x.DiscoveryRunId == runId)
        .OrderBy(x => x.FileName).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    var lines = new List<string> { "FileName,Url,SourcePageUrl,ContentType,FileSizeBytes,Status,DiscoveryMethod" };
    lines.AddRange(rows.Select(x => string.Join(",", Csv(x.FileName), Csv(x.Url), Csv(x.SourcePageUrl),
        Csv(x.ContentType), x.FileSizeBytes?.ToString() ?? "", Csv(x.Status.ToString()), Csv(x.DiscoveryMethod))));
    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv",
        $"discovered-pdfs-{runId}.csv");
});

app.MapGet("/runs/{runId:guid}/accessibility/export", async (
    Guid runId,
    IDbContextFactory<DiscoveryDbContext> factory,
    CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var pages = await db.PageAccessibilityResults.AsNoTracking().Include(x => x.Violations)
        .Where(x => x.DiscoveryRunId == runId).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    var quality = await db.PageQualityResults.AsNoTracking().Where(x => x.DiscoveryRunId == runId)
        .ToDictionaryAsync(x => x.Url, ct);
    var lines = new List<string>
    {
        "PageUrl,Score,ScoringMethod,Critical,Serious,Moderate,Minor,PageError,RuleId,WcagLevel,Impact,NodeCount,Help,HelpUrl,SampleTarget"
    };
    foreach (var page in pages.OrderBy(x => x.Score ?? -1).ThenBy(x => x.Url))
    {
        var prefix = string.Join(",", Csv(page.Url), page.Score?.ToString() ?? "",
            Csv(quality.ContainsKey(page.Url) ? "DubBot-style" : "Legacy weighted"), page.CriticalCount, page.SeriousCount,
            page.ModerateCount, page.MinorCount, Csv(page.Error));
        if (page.Violations.Count == 0)
        {
            lines.Add(prefix + ",,,,,,,");
            continue;
        }

        lines.AddRange(page.Violations
            .OrderByDescending(v => AccessibilityScoring.ImpactWeight(v.Impact)).ThenByDescending(v => v.NodeCount)
            .Select(v => string.Join(",", prefix, Csv(v.RuleId), Csv(v.WcagLevel), Csv(v.Impact), v.NodeCount,
                Csv(v.Help), Csv(v.HelpUrl), Csv(v.SampleTarget))));
    }

    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv",
        $"accessibility-{runId}.csv");
});

app.MapGet("/runs/{runId:guid}/quality/export", async (
    Guid runId,
    IDbContextFactory<DiscoveryDbContext> factory,
    CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var pages = await db.PageQualityResults.AsNoTracking().Where(x => x.DiscoveryRunId == runId).ToListAsync(ct);
    static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    static string Number(double? value) =>
        value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    var lines = new List<string>
    {
        "PageUrl,OverallScore,AccessibilityScore,BrokenLinksScore,SeoScore,BestPracticesScore,LinkCheckError,IssuesJson"
    };
    lines.AddRange(pages.OrderBy(x => x.Url).Select(x => string.Join(",", Csv(x.Url), Number(x.OverallScore),
        Number(x.AccessibilityScore), Number(x.BrokenLinksScore), Number(x.SeoScore), Number(x.BestPracticesScore),
        Csv(x.LinkCheckError), Csv(x.IssuesJson))));
    return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "text/csv",
        $"quality-{runId}.csv");
});

app.MapGet("/runs/{runId:guid}/accessibility/violations/{violationId:guid}/preview", async (
    Guid runId,
    Guid violationId,
    IDbContextFactory<DiscoveryDbContext> factory,
    IssuePreviewStore previews,
    CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    var violation = await db.AccessibilityViolations.AsNoTracking()
        .Include(x => x.PageAccessibilityResult)
        .SingleOrDefaultAsync(x => x.Id == violationId &&
                                  x.PageAccessibilityResult!.DiscoveryRunId == runId, ct);
    if (violation?.PreviewPath is null)
    {
        return Results.NotFound();
    }

    var path = previews.GetFullPath(runId, violationId);
    return File.Exists(path) ? Results.File(path, "image/png") : Results.NotFound();
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
