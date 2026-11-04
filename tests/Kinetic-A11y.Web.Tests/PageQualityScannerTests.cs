using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kinetic.Tests;

public sealed class PageQualityScannerTests
{
    [Theory]
    [InlineData(401, 401, 100, false, "Excluded")]
    [InlineData(405, 401, 100, false, "Excluded")]
    [InlineData(401, 200, 100, false, null)]
    [InlineData(401, 404, 95, false, "Broken Links")]
    [InlineData(404, 404, 95, false, "Broken Links")]
    [InlineData(410, 410, 95, false, "Broken Links")]
    [InlineData(403, 403, null, true, null)]
    [InlineData(429, 429, null, true, null)]
    [InlineData(500, 500, null, true, null)]
    public async Task ScanLinksAsync_ConfirmedStatus_PreservesPublicLinkScoring(
        int headStatus, int getStatus, int? score, bool hasError, string? category)
    {
        using var handler = new LinkHandler(request => new HttpResponseMessage(
            (HttpStatusCode)(request.Method == HttpMethod.Head ? headStatus : getStatus)));
        using var client = new HttpClient(handler);
        var scanner = new PageQualityScanner(NullLogger<PageQualityScanner>.Instance);
        var result = Result();

        await scanner.ScanLinksAsync(result, new Uri(result.Url), ["/target"], client, CancellationToken.None);

        Assert.Equal(score, result.BrokenLinksScore);
        Assert.Equal(hasError, result.LinkCheckError is not null);
        Assert.Equal(score is null ? null : (100 + score) / 2d, result.OverallScore);
        var issues = JsonSerializer.Deserialize<List<QualityIssue>>(result.IssuesJson)!;
        if (category is null)
        {
            Assert.Empty(issues);
        }
        else
        {
            var issue = Assert.Single(issues);
            Assert.Equal(category, issue.Category);
            Assert.Equal("https://example.test/target", issue.Target);
        }
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task ScanLinksAsync_ProtectedAndBrokenLinks_CachesExclusionsWithoutHidingBrokenLinks()
    {
        using var handler = new LinkHandler(request => new HttpResponseMessage(
            request.RequestUri!.AbsolutePath == "/private" ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var scanner = new PageQualityScanner(NullLogger<PageQualityScanner>.Instance);
        foreach (var result in new[] { Result(), Result() })
        {
            await scanner.ScanLinksAsync(result, new Uri(result.Url),
                ["/private", "/private#section", "/missing"], client, CancellationToken.None);

            Assert.Equal(95, result.BrokenLinksScore);
            Assert.Null(result.LinkCheckError);
            var issues = JsonSerializer.Deserialize<List<QualityIssue>>(result.IssuesJson)!;
            Assert.Single(issues, x => x.Category == "Excluded");
            Assert.Single(issues, x => x.Category == "Broken Links");
        }
        Assert.Equal(4, handler.Requests);
    }

    [Fact]
    public async Task ScanLinksAsync_ProtectedLinkAndNetworkFailure_LeavesScoreUnavailable()
    {
        using var handler = new LinkHandler(request => request.RequestUri!.AbsolutePath == "/private"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : throw new HttpRequestException("Network unavailable"));
        using var client = new HttpClient(handler);
        var result = Result();

        await new PageQualityScanner(NullLogger<PageQualityScanner>.Instance).ScanLinksAsync(
            result, new Uri(result.Url), ["/private", "/network"], client, CancellationToken.None);

        Assert.Null(result.BrokenLinksScore);
        Assert.Null(result.OverallScore);
        Assert.Contains("Network unavailable", result.LinkCheckError);
        Assert.DoesNotContain("HTTP 401", result.LinkCheckError);
    }

    [Fact]
    public async Task ScanLinksAsync_Cancelled_DoesNotProduceSuccess()
    {
        using var handler = new LinkHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var result = Result();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PageQualityScanner(NullLogger<PageQualityScanner>.Instance).ScanLinksAsync(
                result, new Uri(result.Url), ["/private"], client, cancellation.Token));

        Assert.Null(result.BrokenLinksScore);
        Assert.Equal(0, handler.Requests);
    }

    private static PageQualityResult Result() => new()
    {
        Url = "https://example.test",
        SeoScore = 100
    };

    private sealed class LinkHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }
}
