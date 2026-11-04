namespace Kinetic;

/// <summary>Configuration for browser-based accessibility analysis.</summary>
public sealed class AccessibilityOptions
{
    /// <summary>Global kill switch; when false, runs never scan accessibility regardless of the per-run setting.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the axe-core tags to run in addition to the separately included best-practice rules.</summary>
    public string[] Tags { get; set; } = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22a", "wcag22aa"];

    /// <summary>Gets or sets the browser page-operation timeout, in seconds.</summary>
    public int PageTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the maximum number of violation records retained for each page.</summary>
    public int MaxViolationsStoredPerPage { get; set; } = 100;

    /// <summary>Gets or sets whether Chromium is downloaded automatically if Playwright cannot find it.</summary>
    public bool AutoInstallBrowsers { get; set; } = true;

    /// <summary>Gets or sets whether Chromium runs without a visible browser window.</summary>
    public bool Headless { get; set; } = true;
}
