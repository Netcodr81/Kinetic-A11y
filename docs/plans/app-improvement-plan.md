# App Improvement Plan

The biggest gains would come from making scan results more actionable and scans more resilient before adding more scoring categories. Existing foundations worth preserving include separate page/link/PDF workers, partial-result preservation, and unavailable scores for inconclusive checks.

## Highest-value improvements

| Priority | Improvement | Current behavior and recommendation |
|---|---|---|
| 1 | Structured diagnostics and retry controls | `PageQualityScanner` stores inconclusive link checks as one long string, which the run page displays in full. Group these into Blocked (403), Rate limited (429), Timeout, Broken (404/410), and Other. Show counts first, expandable URL details, and a "Retry inconclusive checks" action. Keep blocked links separate from confirmed broken links. |
| 2 | Recovery after app restarts | `DiscoveryQueue` exists only in memory, while run statuses are persisted. After an unexpected shutdown, queued work is lost and a run can remain marked Running. Add startup reconciliation to restore queued runs and explicitly mark interrupted runs. True resume would additionally require persisted page/link/PDF job state. |
| 3 | Non-destructive database upgrades | Startup can delete the database after a SQLite exception during its schema probe. Replace automatic destructive recovery with explicit schema upgrades, a backup, and an actionable startup error if upgrading fails. Scan history should not disappear because startup encountered a database problem. |
| 4 | Issue-centric findings | Results are primarily organized by page. Add a view such as "Missing accessible button name - 42 pages," with affected URLs, representative elements, previews, and rule guidance. This helps users fix shared WordPress templates rather than investigate the same issue page by page. |
| 5 | Preserved scan history and comparisons | `DiscoveryRunControl.RestartAsync` deletes the previous run. Add "Run again" that keeps history, then compare new, resolved, and persistent findings. Match findings by normalized page URL and rule ID, not generated database IDs. |

## Correctness and performance improvements

### Consistent URL handling

`WebsiteCrawler` compares entire URLs case-insensitively. Hosts are case-insensitive, but paths and query values can be case-sensitive, so distinct resources may be skipped.

Fast-mode discovery also does not use the HTTP response's final URL after redirects, and only JavaScript mode applies `<base href>` to page discovery. Use consistent resolution rules across both modes.

### Browsable large result sets

`Run.razor` loads all results and violations every two seconds, then displays only 1,000 rows. Add server-side paging, search, severity/category filters, and on-demand violation details.

Compute summaries separately so reducing table data does not change site scores.

### Manual-review findings

`AccessibilityScanner` saves the count of axe's incomplete checks but not their details. Preserve those findings separately and give users a "Needs manual review" queue. Keep them outside confirmed-failure scoring.

### Configuration that matches behavior

`CrawlerOptions` defines `RequestTimeoutSeconds` and `MaxConcurrentRequests`, but the HTTP timeout is hardcoded in `Program.cs`, and the crawler uses fixed sequential workers.

Wire supported settings into the implementation or remove misleading settings. Before increasing concurrency, add per-host pacing, resource limits, and bounded job queues.

### Durable regression coverage

Add a committed test project and CI workflow covering:

- Blocked root pages.
- CSS exclusion and PDF discovery.
- Browser-driver disconnects.
- Cancellation during database writes.
- Sitemap recovery.
- Unavailable-score calculations.

Use local HTTP fixtures so these checks do not depend on Cloudflare's behavior.

## Product and accessibility polish

### Check coverage and progress

Show separate progress for pages, link checks, and PDF validation. Distinguish pending, disabled, failed, and blocked checks instead of making users interpret N/A.

### Accessibility of the app itself

`App.razor` lacks a main landmark, and the home-page validation message is not associated with its input. Add accessible validation, route-focus management, and a pause control for automatic result refresh.

### Generated evidence and retention

Preview PNGs under `App_Data/issue-previews` are still tracked despite the ignore rule. Untrack them while preserving local files, and add retention/storage limits for screenshots and scan data.

### Precise PDF-validation labeling

The app checks whether a URL appears to serve a PDF; it does not assess PDF accessibility. Make that distinction explicit in the results.

## Recommended starting order

Start with structured diagnostics and retry controls, followed by restart recovery and non-destructive database upgrades. These changes directly address the confusing failures encountered so far and improve dependability before expanding the feature set.
