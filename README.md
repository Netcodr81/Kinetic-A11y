# Kinetic-A11y

Kinetic-A11y is a .NET 10 Blazor Server app that discovers publicly linked PDF documents on a website and can run automated accessibility checks on the HTML pages it crawls.

## Features

- Crawl a website breadth-first, staying on the root host by default.
- Read `robots.txt` for sitemap declarations and check common sitemap locations.
- Find PDF links in HTML links, frames, embeds, objects, sources, and common URL data attributes, as well as sitemaps.
- Check discovered PDF URLs with HTTP requests and report them as valid, unverified, or unavailable. The app does not intentionally download entire PDFs.
- Persist crawl runs and results in SQLite; view progress and results in the web UI and export PDFs or accessibility results as CSV.
- Optionally scan crawled pages with axe-core in headless Chromium for WCAG 2.0/2.1 Level A and AA rules.

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
| `Accessibility` | `Tags` | axe-core rule tags to run. Defaults to WCAG 2.0/2.1 A and AA. |
| `Accessibility` | `PageTimeoutSeconds` | Browser navigation and page-operation timeout. |
| `Accessibility` | `ScoreDecayConstant` | Score decay factor (default 50). |
| `Accessibility` | `MaxViolationsStoredPerPage` | Maximum violation records saved per page (default 100). |
| `Accessibility` | `AutoInstallBrowsers` / `Headless` | Automatically install Chromium when missing and run it without a visible window. |

The page score is `round(100 × e^(-penalty / decay))`, where penalty sums impact weight × affected node count (critical 10, serious 5, moderate 3, minor 1). The run score is the average of successfully scored pages; scores of 90+ are marked good, 50–89 fair, and below 50 poor.

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
