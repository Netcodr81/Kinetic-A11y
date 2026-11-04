# Copilot instructions

## Build, test, and run

Run commands from the repository root. The project targets .NET 10.

```powershell
dotnet restore Kinetic-A11y.slnx
dotnet build Kinetic-A11y.slnx
dotnet run --project src/Kinetic-A11y.Web/Kinetic-A11y.Web.csproj
```

There is currently no test project in the solution. If one is added, run all tests with `dotnet test Kinetic-A11y.slnx` and a single test with:

```powershell
dotnet test <test-project.csproj> --filter "FullyQualifiedName~Namespace.ClassName.TestName"
```

There is no repository-specific linter or formatting configuration. The .NET SDK formatter can check formatting with `dotnet format Kinetic-A11y.slnx --verify-no-changes`.

The repository-level VS Code MCP configuration at `.vscode/mcp.json` launches Playwright MCP through `npx`; Node.js must be available. Use it for browser-based validation of the local Blazor UI. The app itself uses the Playwright .NET library for its scanner.

## Architecture

- `src/Kinetic-A11y.Web` is a Blazor Server app. `Components/Pages/Home.razor` validates settings, saves a `DiscoveryRun`, and enqueues its ID; `Run.razor` polls persisted state and provides cancellation, restart, deletion, and exports.
- `Program.cs` wires up the interactive server UI, SQLite `DiscoveryDbContext`, named crawler HTTP client, options, scanners, run-control services, and the hosted `DiscoveryWorker`.
- `DiscoveryWorker` consumes the in-memory FIFO `DiscoveryQueue`, creates a DI scope per run, and delegates lifecycle and cancellation to `DiscoveryRunControl`. `WebsiteCrawler` performs breadth-first page discovery and sitemap processing. During a run, page discovery, link checking, and PDF validation use separate sequential workers, with link and PDF jobs processed concurrently with page crawling.
- Fast mode fetches server HTML and parses it with AngleSharp. Optional JavaScript mode uses Chromium through Playwright; rendered HTML feeds page/PDF discovery and quality analysis, and accessibility scanning can reuse that browser page. `AccessibilityScanner` maps axe-core findings; `PageQualityScanner` checks SEO metadata and HTTP(S) links.
- `Data/DiscoveryDbContext.cs` and `Models/` define persisted runs, PDFs, accessibility findings, and page-quality results. SQLite is initialized with `EnsureCreated`; schema additions are handled by explicit additive helpers in the context. Review the existing startup compatibility logic before changing schemas, because an outdated core schema can trigger database recreation.

## Repository-specific conventions

- Source files use the `Kinetic` namespace, including the web components' backing C# code.
- Bind configuration sections to typed options (`CrawlerOptions`, `AccessibilityOptions`) and register services in `Program.cs`. Use `IDbContextFactory<DiscoveryDbContext>` to create short-lived contexts, especially in background work; do not share the crawler's context with link/PDF workers.
- Carry cancellation tokens through asynchronous crawling, browser operations, queue workers, and database writes. A run must not be deleted or restarted until its active workers have stopped; preserve partial saved results when cancelled.
- Normalize crawl/result URLs consistently: remove fragments and trim trailing slashes except at the host root. The root host is allowed by default; additional crawl hosts come from `Crawler:AllowedHosts`.
- Quality scores count distinct issue/rule IDs, not affected elements. Impact severity orders accessibility findings but does not change the score. Keep failed or inconclusive checks unavailable (`null`/N/A), rather than treating them as successful.
- Keep SQLite compatibility additive where possible. `EnsureCreated` does not apply migrations, and startup contains an explicit destructive recovery path for an outdated core schema.
- The app accepts user-supplied URLs and has no authentication. Preserve the README's trusted-use and outbound-network precautions; URL scheme validation alone is not a complete SSRF defense.

## Mandatory Grill-Me Planning Workflow

When developing a plan, Copilot MUST use the Grill-Me skill located at:

`skills/grill-me/SKILL.md`

### When to Invoke Grill-Me

Invoke Grill-Me whenever the user requests:

- An implementation or development plan
- Application or system architecture
- Feature planning
- Refactoring or restructuring plans
- Technical design proposals
- Implementation roadmaps
- Technical specifications involving architectural or implementation decisions
- Planning or evaluating multiple implementation approaches

### Required Workflow

1. Read `skills/grill-me/SKILL.md` before beginning the planning process.
2. Follow the skill's instructions, including its questioning, requirements discovery, and decision-making process.
3. Inspect the existing repository and relevant documentation before asking questions.
4. Use existing conversation context and repository information to avoid redundant questions.
5. Do not prematurely generate an implementation plan before completing the required Grill-Me workflow.
6. Once planning is complete, produce the requested plan using the established requirements and decisions.
7. Do not begin implementation unless the user requests it.

### Exceptions

Do not invoke Grill-Me for routine coding, straightforward explanations, or tasks that do not require planning.

If the user explicitly requests that Grill-Me not be used, respect that instruction.

### Skill Integrity

- Treat `skills/grill-me/SKILL.md` as the authoritative source for the Grill-Me workflow.
- Do not duplicate or independently redefine the skill's planning methodology.
- If the skill cannot be accessed, report the issue rather than silently substituting another planning process.