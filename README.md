# Kinetic-A11y

Kinetic-A11y is a .NET 10 Blazor Server app that discovers publicly linked PDF documents on a website and can run automated accessibility checks on the HTML pages it crawls.

## Features

- Crawl a website breadth-first, staying on the root host by default.
- Read `robots.txt` for sitemap declarations and check common sitemap locations.
- Find PDF links in HTML links, frames, embeds, objects, sources, and common URL data attributes, as well as sitemaps.
- Check discovered PDF URLs with HTTP requests and report them as valid, unverified, or unavailable. The app does not intentionally download entire PDFs.
- Persist crawl runs and results in SQLite; view progress and results in the web UI and export PDFs or accessibility results as CSV.
- Optionally scan crawled pages with axe-core in headless Chromium using configurable WCAG version and conformance-level tags. The checked-in configuration selects WCAG 2.0/2.1/2.2 A/AA tags, additional AAA tags, and Section 508 checks, with separate Best Practices results.
- Score SEO and broken links on crawled HTML pages, and show per-page category scores and an overall site score.

Automated accessibility checks cover only a subset of accessibility issues. The 0–100 score is a prioritization heuristic, not a compliance determination.

## Requirements and running locally

- .NET 10 SDK
- Internet access from the machine running the app and crawler
- Playwright Chromium for accessibility scans (installed automatically on first use by default)

The app references `Microsoft.Playwright` directly so it does not use axe-core's older minimum dependency. Playwright package updates require the matching Chromium build; the default automatic installation handles this on the next scan. If the browser or driver disconnects, the scanner creates a fresh browser and driver for the next page. The affected page retains its error rather than being reported as successfully checked.

From the repository root:

```bash
dotnet restore src/Kinetic-A11y.Web/Kinetic-A11y.Web.csproj
dotnet run --project src/Kinetic-A11y.Web/Kinetic-A11y.Web.csproj
```

Open the local URL printed by ASP.NET Core. The app creates `pdf-discovery.db` in its working directory unless the `ConnectionStrings:Discovery` setting points elsewhere.

The root URL counts as a discovered page even if loading it fails. Root-page errors, including HTTP status codes, are shown on the run; a run with no successfully loaded HTML pages is marked Failed rather than Completed. Some sites (including Cloudflare-protected sites) reject direct crawler requests with HTTP 403. Try the optional JavaScript-rendered mode for sites that load in a browser, but browser rendering does not guarantee access through bot protection. If access remains blocked, ask the site administrator to allow the scanner.

On the home page, enter an absolute HTTP or HTTPS root URL, choose a maximum crawl depth (default 5) and page limit (default 2,000), and optionally turn off accessibility scanning. The accepted UI limits are depth 0–20 and 1–100,000 pages. Runs execute in a background worker; the run page refreshes while the crawl is active.

Use **Cancel scan** on the run page to stop a queued or running scan, retaining its saved results. Once stopped (or completed or failed), **Start again** permanently deletes the previous run, its PDFs, and accessibility results, and creates a fresh run with the same URL, crawl limits, and accessibility setting. It does not resume the previous crawl.

Use **Delete scan**, then **Confirm delete**, to permanently remove a run and all its associated PDF records, page accessibility results, and violations without starting another run. Active work is stopped before deletion. **Keep scan** dismisses the confirmation without changing the run.

The Discovered PDFs, Site and page quality scores, and Page accessibility tables have independent paging controls. Each defaults to 25 rows per page; select 25, 50, or 100 rows and use First, Previous, Next, or Last to navigate all saved results. Changing the page size returns that table to its first page. Live refreshes retain each table's selected page and page size. Summary scores and counts still cover the whole run, and CSV exports include all results regardless of the displayed page.

## JavaScript-rendered scans

Select **JavaScript-rendered scan (slower)** before starting a run for sites that populate content using JavaScript. Set **Wait after page load** from 0 to 60 seconds (default 2). Chromium waits for the initial HTML DOM to load, then allows the configured extra time for scripts and API-driven rendering. The rendered HTML supplies page/PDF discovery, SEO, and link checks; accessibility checks run in that same browser page after the wait. Accessibility remains independently optional, but JavaScript mode requires Chromium even when accessibility is off.

Fast server-HTML mode remains the default and preserves the existing behavior. Rendering settings are saved per run and copied by **Start again**. Existing runs receive fast-mode defaults without losing data. Browser navigation is bounded by `Accessibility:PageTimeoutSeconds`; the extra wait is separate and cancellable. Rendering errors are displayed on the run and affected accessibility page, rather than silently falling back to unrendered HTML or reporting a successful score.

This is a fixed rendering allowance, not a guarantee that all JavaScript has finished. Increase it for slower sites. The crawler does not click controls or exercise every interactive state; hash-only routes are still not crawled separately, and shadow-DOM-only links are not extracted. JavaScript mode executes site scripts and may make third-party requests, so existing trusted-user and outbound-network precautions still apply. TLS errors, blocked requests, and failed link checks are not fixed by adding a wait.

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
| `Accessibility` | `Tags` | axe-core rule tags selecting scan versions and levels. The checked-in list includes WCAG 2.0/2.1/2.2 A/AA, `wcag2aaa`, `wcag21aaa`, and `section508`. See [Accessibility scan levels](#accessibility-scan-levels). Best Practices checks are always included. |
| `Accessibility` | `PageTimeoutSeconds` | Browser navigation and page-operation timeout. |
| `Accessibility` | `MaxViolationsStoredPerPage` | Maximum violation records saved per page (default 100). |
| `Accessibility` | `CaptureIssuePreviews` | Whether to save screenshots with the affected element outlined. |
| `Accessibility` | `IssuePreviewDirectory` | Directory for preview PNG files, relative to the app content root unless absolute. |
| `Accessibility` | `MaxIssuePreviewsPerPage` | Maximum preview images captured for a page (default 10). |
| `Accessibility` | `AutoInstallBrowsers` / `Headless` | Automatically install Chromium when missing and run it without a visible window. |

Issue previews are captured for the highest-impact stored WCAG violations when their target can be located in the scanned page. PNG files are stored outside the web root; only their generated relative paths are stored in SQLite. Previews are removed when a run is deleted or replaced. Screenshots can contain sensitive page content, so keep the app and preview directory within a trusted environment.

## Accessibility scan levels

`Accessibility:Tags` selects groups of axe-core rules, not a certification target. WCAG conformance levels are cumulative: Level AA includes Level A requirements, and Level AAA includes A and AA. Tags themselves select rule groups; selecting a newer version or higher level alone does not automatically include all earlier-version and lower-level groups.

| Scan scope | Tags |
|---|---|
| WCAG 2.0 Level A | `wcag2a` |
| WCAG 2.0 Level AA additions | `wcag2aa` |
| WCAG 2.1 Level A additions | `wcag21a` |
| WCAG 2.1 Level AA additions | `wcag21aa` |
| WCAG 2.2 Level A additions | `wcag22a` |
| WCAG 2.2 Level AA additions | `wcag22aa` |
| Additional AAA groups selected in the checked-in configuration | `wcag2aaa`, `wcag21aaa` |
| Section 508 rule group | `section508` |

For a WCAG 2.2 A/AA scan, retain all six A/AA tags above. For WCAG 2.1 A/AA, use `wcag2a`, `wcag2aa`, `wcag21a`, and `wcag21aa`. For an A-only scan across the configured versions, use `wcag2a`, `wcag21a`, and `wcag22a`. To include AAA checks, retain the applicable A/AA groups and add AAA tags supported by the bundled axe-core version. A tag only runs the automated rules available for that group; some groups may have no applicable rules.

The current settings repeat `wcag2aaa`; the scanner removes duplicate tags before execution, so it does not run or count those checks twice. The list does not include `wcag22aaa` and should not be described as a complete WCAG 2.2 AAA scan. Tag availability and automated coverage depend on the bundled axe-core version; adding a tag does not add new rules to the engine. Section 508 selection likewise does not establish Section 508 compliance.

### Adjusting the scan scope

1. Stop or finish active scans before changing the scope.
2. Edit the `Accessibility` section's `Tags` array in `src/Kinetic-A11y.Web/appsettings.json`. Use the combinations above to choose the desired versions and levels; add or remove `section508` independently. Check for environment-specific configuration overrides if the effective settings differ.
3. Restart the application. The scanner reads its options when it is created; editing the file does not switch the scope of the existing scanner.
4. Start a new scan to collect results with the updated tags. Existing results are not recalculated. **Start again** also uses the current application configuration, but permanently replaces the previous run and its results.

Tags are application-wide settings, not a per-run selector in the UI, and the selected tag list is not saved with each run. Record the configuration separately when comparing scans. `Accessibility:Enabled` must be `true` and accessibility scanning must be enabled for the run; tag selection does not override either switch.

The scanner always adds `best-practice`, even if it is omitted from `Tags`. Findings with any `wcag` tag contribute to the Accessibility score; selected findings without a `wcag` tag are reported under Best Practices. Consequently, a Section 508 finding contributes to Accessibility only if it also carries a WCAG tag; otherwise it appears under Best Practices, which is excluded from the overall score.

Changing the selected rules can change scores without any website changes, so compare scores only with the scan scope in mind. Automated checks cover only part of any conformance level and do not replace manual review. A score of 100 does not establish WCAG or Section 508 compliance, including at AAA.

## DubBot-style scoring

Scoring follows [DubBot's published methodology](https://help.dubbot.com/en/articles/11464984-scoring): each page's category score is `max(0, 100 - 5 * distinct issue types)`. One accessibility rule failing on seven elements counts once, not seven times. Impact severity only controls display order. Twenty or more issue types produce a score of 0. Accessibility and Best Practices are classified separately using axe-core WCAG tags; incomplete checks are not counted as confirmed violations.

Site category scores are unrounded arithmetic averages of successfully checked pages. The overall site score averages Accessibility, Broken Links, and SEO category scores, excluding Best Practices. Accessibility is omitted when scanning is disabled. A page's overall score averages the same enabled categories; it is unavailable if any enabled category failed. Site scores are unavailable until every enabled category has at least one successful result. Failed checks are excluded from category averages, not treated as perfect scores; coverage is shown and scores are provisional during a scan or with incomplete coverage. The site average uses all saved quality pages, not just the current table page.

Page crawling/accessibility, link checking, and PDF validation use three independent, sequential workers per run. Slow link checks or PDF responses do not block the next page scan. Link-check jobs retain only URLs, not browser pages or full HTML documents. PDFs are saved as Pending as soon as they are discovered; validation updates their status separately. Sitemap discovery runs after the HTML-linked page queue is drained, then adds any new sitemap-only pages and PDFs within the crawl limits.

SEO and accessibility results are saved immediately. Broken Links and overall scores remain N/A while those checks are pending. The run shows the page-discovery activity and URL; link checks and PDF validation may continue in parallel. Once page discovery finishes, the run reports that it is waiting for those workers and stays Running until all work finishes. Cancelling stops and awaits all workers, preserving saved results and leaving unfinished PDF validation Pending rather than claiming success. Deletion and restart still wait for all writes to stop.

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

Only navigation links, frames, and URL data attributes supply HTML page candidates. Resource tags such as stylesheet links, embeds, objects, and sources still supply PDF candidates, but are not treated as page navigation. Common asset and document extensions are excluded from page crawling, including sitemap entries; extensionless URLs and dynamic page extensions remain eligible.

The crawler discovers links from server HTML (or rendered HTML in optional JavaScript mode) and sitemaps. It does not search authenticated content, submit forms, explore POST-only endpoints, or guarantee discovery of unlinked files. Sitemap discovery is best-effort, and some servers may not support the requests used to verify PDF URLs.

Only run discovery against sites you are authorized to access. The app has no authentication or authorization and accepts user-supplied URLs. URL validation limits the scheme to HTTP(S), but is not a complete SSRF defense; redirects, DNS rebinding, and browser-loaded subresources can reach destinations beyond the initial URL. Do not expose the app publicly or use it against untrusted URL submissions. Apply network-level egress restrictions and appropriate access controls before any deployment beyond a trusted local environment, and observe site terms, robots directives, and rate limits.
