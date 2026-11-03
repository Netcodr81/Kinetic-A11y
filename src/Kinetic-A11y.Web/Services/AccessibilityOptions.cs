namespace Kinetic;

public sealed class AccessibilityOptions
{
    /// <summary>Global kill switch; when false, runs never scan accessibility regardless of the per-run setting.</summary>
    public bool Enabled { get; set; } = true;
    public string[] Tags { get; set; } = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22a", "wcag22aa"];
    public int PageTimeoutSeconds { get; set; } = 30;
    public int MaxViolationsStoredPerPage { get; set; } = 100;
    public bool AutoInstallBrowsers { get; set; } = true;
    public bool Headless { get; set; } = true;
}
