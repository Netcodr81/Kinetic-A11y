using Microsoft.EntityFrameworkCore;

namespace Kinetic;

public sealed class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options) : DbContext(options)
{
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();
    public DbSet<DiscoveredPdf> DiscoveredPdfs => Set<DiscoveredPdf>();
    public DbSet<PageAccessibilityResult> PageAccessibilityResults => Set<PageAccessibilityResult>();
    public DbSet<AccessibilityViolation> AccessibilityViolations => Set<AccessibilityViolation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
}
