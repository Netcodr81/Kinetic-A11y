using Microsoft.EntityFrameworkCore;

namespace Kinetic;

public sealed class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options) : DbContext(options)
{
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();
    public DbSet<DiscoveredPdf> DiscoveredPdfs => Set<DiscoveredPdf>();
    public DbSet<PageAccessibilityResult> PageAccessibilityResults => Set<PageAccessibilityResult>();
    public DbSet<AccessibilityViolation> AccessibilityViolations => Set<AccessibilityViolation>();
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
            b.Property(x => x.SampleTarget).HasMaxLength(1024);
            b.Property(x => x.SampleHtml).HasMaxLength(1024);
        });
    }

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
