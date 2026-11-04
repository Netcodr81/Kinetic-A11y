using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kinetic.Tests;

public sealed class WebsiteCrawlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_UnauthorizedPage_ExcludesResultsAndFailures(bool protectedRoot)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kinetic-auth-test-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new TestDbFactory(new DbContextOptionsBuilder<DiscoveryDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False").Options);
            await using var db = factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            var run = new DiscoveryRun
            {
                RootUrl = protectedRoot ? "https://example.test/private" : "https://example.test",
                ScanAccessibility = false,
                MaxDepth = 1
            };
            db.DiscoveryRuns.Add(run);
            await db.SaveChangesAsync();
            using var handler = new CrawlHandler();
            using var client = new HttpClient(handler);
            var settings = Options.Create(new AccessibilityOptions { Enabled = false });
            var previews = new IssuePreviewStore(settings, new TestEnvironment(), NullLogger<IssuePreviewStore>.Instance);
            await using var accessibility = new AccessibilityScanner(settings, previews, NullLogger<AccessibilityScanner>.Instance);
            var crawler = new WebsiteCrawler(new TestHttpFactory(client), factory,
                Options.Create(new CrawlerOptions { CheckSitemaps = false, DelayMilliseconds = 0 }),
                accessibility, new PageQualityScanner(NullLogger<PageQualityScanner>.Instance),
                NullLogger<WebsiteCrawler>.Instance);

            if (protectedRoot)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => crawler.CrawlAsync(run.Id, CancellationToken.None));
                Assert.Contains("requires authentication", error.Message);
            }
            else await crawler.CrawlAsync(run.Id, CancellationToken.None);

            await db.Entry(run).ReloadAsync();
            Assert.Equal(0, run.FailedRequests);
            Assert.Equal(0, run.AccessibilityScanFailures);
            Assert.Equal(protectedRoot ? 0 : 1, run.PagesCrawled);
            Assert.Empty(await db.PageAccessibilityResults.ToListAsync());
            var quality = await db.PageQualityResults.ToListAsync();
            if (protectedRoot) Assert.Empty(quality);
            else
            {
                var page = Assert.Single(quality);
                Assert.Equal(100, page.BrokenLinksScore);
                Assert.Equal(100d, page.OverallScore);
                Assert.Contains("Excluded", page.IssuesJson);
                Assert.Null(page.LinkCheckError);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CrawlHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/private"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><head><title>Public</title><meta name='description' content='Public page'></head><body><a href='/private'>Private</a></body></html>",
                        System.Text.Encoding.UTF8, "text/html")
                });
    }

    private sealed class TestHttpFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestDbFactory(DbContextOptions<DiscoveryDbContext> options) : IDbContextFactory<DiscoveryDbContext>
    {
        public DiscoveryDbContext CreateDbContext() => new(options);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Kinetic.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
