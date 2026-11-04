using Microsoft.EntityFrameworkCore;

namespace Kinetic;

/// <summary>
/// Provides Entity Framework access to discovery runs and their persisted results.
/// </summary>
public sealed class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options) : DbContext(options)
{
    /// <summary>Gets the website discovery runs stored in the database.</summary>
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();

    /// <summary>Gets the PDF documents found by discovery runs.</summary>
    public DbSet<DiscoveredPdf> DiscoveredPdfs => Set<DiscoveredPdf>();

    /// <summary>Gets the accessibility scan results for crawled pages.</summary>
    public DbSet<PageAccessibilityResult> PageAccessibilityResults => Set<PageAccessibilityResult>();

    /// <summary>Gets the individual accessibility violations reported for pages.</summary>
    public DbSet<AccessibilityViolation> AccessibilityViolations => Set<AccessibilityViolation>();

    /// <summary>Gets the SEO, accessibility, and link-quality results for pages.</summary>
    public DbSet<PageQualityResult> PageQualityResults => Set<PageQualityResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PageQualityResult>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DiscoveryRunId, x.Url }).IsUnique();
            b.HasOne(x => x.DiscoveryRun).WithMany().HasForeignKey(x => x.DiscoveryRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DiscoveryRun>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.RootUrl).HasMaxLength(2048);
            b.HasMany(x => x.Documents).WithOne(x => x.DiscoveryRun)
                .HasForeignKey(x => x.DiscoveryRunId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DiscoveredPdf>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Url).HasMaxLength(2048);
            b.Property(x => x.SourcePageUrl).HasMaxLength(2048);
            b.HasIndex(x => new { x.DiscoveryRunId, x.Url }).IsUnique();
        });
        modelBuilder.Entity<DiscoveryRun>()
            .HasMany(x => x.AccessibilityResults).WithOne(x => x.DiscoveryRun)
            .HasForeignKey(x => x.DiscoveryRunId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PageAccessibilityResult>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Url).HasMaxLength(2048);
            b.Property(x => x.Error).HasMaxLength(2048);
            b.HasIndex(x => new { x.DiscoveryRunId, x.Url }).IsUnique();
            b.HasMany(x => x.Violations).WithOne(x => x.PageAccessibilityResult)
                .HasForeignKey(x => x.PageAccessibilityResultId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccessibilityViolation>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.RuleId).HasMaxLength(256);
            b.Property(x => x.Impact).HasMaxLength(32);
            b.Property(x => x.HelpUrl).HasMaxLength(2048);
            b.Property(x => x.WcagLevel).HasMaxLength(8);
            b.Property(x => x.SampleTarget).HasMaxLength(1024);
            b.Property(x => x.SampleHtml).HasMaxLength(1024);
            b.Property(x => x.PreviewPath).HasMaxLength(512);
        });
    }

    /// <summary>
    /// Adds JavaScript-rendering columns to an existing SQLite discovery-runs table when absent.
    /// </summary>
    public async Task EnsureJavaScriptSchemaAsync()
    {
        await Database.OpenConnectionAsync();
        try
        {
            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('DiscoveryRuns')";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            if (!columns.Contains(nameof(DiscoveryRun.RenderJavaScript)))
                await Database.ExecuteSqlRawAsync("ALTER TABLE DiscoveryRuns ADD COLUMN RenderJavaScript INTEGER NOT NULL DEFAULT 0");
            if (!columns.Contains(nameof(DiscoveryRun.JavaScriptWaitSeconds)))
                await Database.ExecuteSqlRawAsync("ALTER TABLE DiscoveryRuns ADD COLUMN JavaScriptWaitSeconds INTEGER NOT NULL DEFAULT 2");
            if (!columns.Contains(nameof(DiscoveryRun.CurrentActivity)))
                await Database.ExecuteSqlRawAsync("ALTER TABLE DiscoveryRuns ADD COLUMN CurrentActivity TEXT NULL");
        }
        finally { await Database.CloseConnectionAsync(); }
    }

    /// <summary>Adds the issue-preview location column to an existing accessibility violations table when absent.</summary>
    public async Task EnsureIssuePreviewSchemaAsync()
    {
        await Database.OpenConnectionAsync();
        try
        {
            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('AccessibilityViolations')";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            if (!columns.Contains(nameof(AccessibilityViolation.PreviewPath)))
                await Database.ExecuteSqlRawAsync("ALTER TABLE AccessibilityViolations ADD COLUMN PreviewPath TEXT NULL");
        }
        finally { await Database.CloseConnectionAsync(); }
    }

    /// <summary>Adds the WCAG level column to an existing accessibility violations table when absent.</summary>
    public async Task EnsureWcagLevelSchemaAsync()
    {
        await Database.OpenConnectionAsync();
        try
        {
            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('AccessibilityViolations')";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            if (!columns.Contains(nameof(AccessibilityViolation.WcagLevel)))
                await Database.ExecuteSqlRawAsync("ALTER TABLE AccessibilityViolations ADD COLUMN WcagLevel TEXT NULL");
        }
        finally { await Database.CloseConnectionAsync(); }
    }

    /// <summary>Creates the additive page-quality results table and its unique run/URL index if needed.</summary>
    public Task EnsureQualitySchemaAsync() => Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "PageQualityResults" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_PageQualityResults" PRIMARY KEY,
            "DiscoveryRunId" TEXT NOT NULL,
            "Url" TEXT NOT NULL,
            "AccessibilityEnabled" INTEGER NOT NULL,
            "AccessibilityScore" INTEGER NULL,
            "BestPracticesScore" INTEGER NULL,
            "BrokenLinksScore" INTEGER NULL,
            "SeoScore" INTEGER NULL,
            "IssuesJson" TEXT NOT NULL,
            "LinkCheckError" TEXT NULL,
            FOREIGN KEY ("DiscoveryRunId") REFERENCES "DiscoveryRuns" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PageQualityResults_DiscoveryRunId_Url"
            ON "PageQualityResults" ("DiscoveryRunId", "Url");
        """);
}
