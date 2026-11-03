namespace Kinetic;

public sealed class AccessibilityOptions
{
    /// <summary>Global kill switch; when false, runs never scan accessibility regardless of the per-run setting.</summary>
    public bool Enabled { get; set; } = true;
    public string[] Tags { get; set; } = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];
    public int PageTimeoutSeconds { get; set; } = 30;
    /// <summary>Controls how quickly the score decays as weighted violations accumulate (higher is more lenient).</summary>
    public double ScoreDecayConstant { get; set; } = 50;
    public int MaxViolationsStoredPerPage { get; set; } = 100;
    public bool AutoInstallBrowsers { get; set; } = true;
    public bool Headless { get; set; } = true;
}
