# Kinetic-A11y

Kinetic-A11y is a .NET 10 Blazor Server app that discovers publicly linked PDF documents on a website and can run automated accessibility checks on the HTML pages it crawls.

## Features

- Crawl a website breadth-first, staying on the root host by default.
- Read `robots.txt` for sitemap declarations and check common sitemap locations.
- Find PDF links in HTML links, frames, embeds, objects, sources, and common URL data attributes, as well as sitemaps.
- Check discovered PDF URLs with HTTP requests and report them as valid, unverified, or unavailable. The app does not intentionally download entire PDFs.
- Persist crawl runs and results in SQLite; view progress and results in the web UI and export PDFs or accessibility results as CSV.
- Optionally scan crawled pages with axe-core in headless Chromium for WCAG 2.0/2.1/2.2 Level A and AA rules, with separate Best Practices results.
- Score SEO and broken links on crawled HTML pages, and show per-page category scores and an overall site score.

Automated accessibility checks cover only a subset of accessibility issues. The 0–100 score is a prioritization heuristic, not a compliance determination.

## Requirements and running locally

- .NET 10 SDK
- Internet access from the machine running the app and crawler
- Playwright Chromium for accessibility scans (installed automatically on first use by default)

From the repository root:

```bash
dotnet restore src/Kinetic-A11y.Web/Kinetic-A11y.Web.csproj
dotnet run --project src/Kinetic-A11y.Web/Kinetic-A11y.Web.csproj
```

Open the local URL printed by ASP.NET Core. The app creates `pdf-discovery.db` in its working directory unless the `ConnectionStrings:Discovery` setting points elsewhere.

On the home page, enter an absolute HTTP or HTTPS root URL, choose a maximum crawl depth (default 5) and page limit (default 2,000), and optionally turn off accessibility scanning. The accepted UI limits are depth 0–20 and 1–100,000 pages. Runs execute in a background worker; the run page refreshes while the crawl is active.

Use **Cancel scan** on the run page to stop a queued or running scan, retaining its saved results. Once stopped (or completed or failed), **Start again** permanently deletes the previous run, its PDFs, and accessibility results, and creates a fresh run with the same URL, crawl limits, and accessibility setting. It does not resume the previous crawl.

Use **Delete scan**, then **Confirm delete**, to permanently remove a run and all its associated PDF records, page accessibility results, and violations without starting another run. Active work is stopped before deletion. **Keep scan** dismisses the confirmation without changing the run.

## Configuration

Settings are in `src/Kinetic-A11y.Web/appsettings.json`:

| Section | Setting | Purpose |
|---|---|---|
| `ConnectionStrings` | `Discovery` | SQLite connection string. |
| `Crawler` | `DelayMilliseconds` | Delay between crawled page requests (default 500 ms). |
| `Crawler` | `RequestTimeoutSeconds` | HTTP client timeout (default 25 seconds). |
| `Crawler` | `AllowedHosts` | Additional hosts the HTML crawler may follow. |
| `Crawler` | `CheckSitemaps` | Whether to inspect `robots.txt` and common sitemap URLs. |
| `Accessibility` | `Enabled` | Global switch for accessibility scanning. |
| `Accessibility` | `Tags` | axe-core rule tags to run. Defaults to WCAG 2.0/2.1/2.2 A and AA; Best Practices checks are also run separately. |
| `Accessibility` | `PageTimeoutSeconds` | Browser navigation and page-operation timeout. |
| `Accessibility` | `MaxViolationsStoredPerPage` | Maximum violation records saved per page (default 100). |
| `Accessibility` | `AutoInstallBrowsers` / `Headless` | Automatically install Chromium when missing and run it without a visible window. |

## DubBot-style scoring

Scoring follows [DubBot's published methodology](https://help.dubbot.com/en/articles/11464984-scoring): each page's category score is `max(0, 100 - 5 * distinct issue types)`. One accessibility rule failing on seven elements counts once, not seven times. Impact severity only controls display order. Twenty or more issue types produce a score of 0. Accessibility and Best Practices are classified separately using axe-core WCAG tags; incomplete checks are not counted as confirmed violations.

Site category scores are unrounded arithmetic averages of successfully checked pages. The overall site score averages Accessibility, Broken Links, and SEO category scores, excluding Best Practices. Accessibility is omitted when scanning is disabled. A page's overall score averages the same enabled categories; it is unavailable if any enabled category failed. Site scores are unavailable until every enabled category has at least one successful result. Failed checks are excluded from category averages, not treated as perfect scores; coverage is shown and scores are provisional during a scan or with incomplete coverage. The site average uses all saved quality pages, not just the 1,000 displayed in the table.

SEO currently checks for a nonempty title and meta description. Broken-link checks cover distinct HTTP(S) anchor/area destinations, including external links, resolve HTML base URLs, ignore fragments, and cache checks within a run. A failing HEAD request is confirmed with GET (without reading the body); confirmed 404/410 responses count as broken URLs. Timeouts, network errors, authorization failures, rate limits, and other non-success responses are inconclusive and make that page's broken-link score unavailable. Repeated links to the same URL count once. These requests add crawl time and outbound traffic.

The formula matches DubBot's published scoring, not its proprietary detection engine or full product coverage. Spelling and Web Governance are not implemented or included in the overall score. Results and issue details can be exported from `/runs/{id}/quality/export`; the accessibility export identifies legacy versus new-format scores.

Existing scores retain their legacy weighted formula and are labeled accordingly. A new additive quality-results table is created without deleting existing runs; start a fresh run to obtain the new scores. Deleting or replacing a run also deletes its quality results. The old `ScoreDecayConstant` setting no longer applies.

The app uses EF Core `EnsureCreated`, not migrations. If the existing database is missing the current accessibility schema, startup recreates it; this deletes existing discovery data.

## Project layout

```text
Kinetic-A11y.slnx
src/
  Kinetic-A11y.Web/
    Components/       Blazor pages and layout
    Data/             EF Core SQLite context
    Models/           Discovery and accessibility result models
    Services/         Crawler, background worker, and accessibility scanner
```

## Scope and safety

The crawler discovers links that are present in fetched HTML or sitemaps; it does not execute page JavaScript or search authenticated content, form submissions, POST-only endpoints, or unlinked files. Sitemap discovery is best-effort, and some servers may not support the requests used to verify PDF URLs.

Only run discovery against sites you are authorized to access. The app has no authentication or authorization and accepts user-supplied URLs. URL validation limits the scheme to HTTP(S), but is not a complete SSRF defense; redirects, DNS rebinding, and browser-loaded subresources can reach destinations beyond the initial URL. Do not expose the app publicly or use it against untrusted URL submissions. Apply network-level egress restrictions and appropriate access controls before any deployment beyond a trusted local environment, and observe site terms, robots directives, and rate limits.
