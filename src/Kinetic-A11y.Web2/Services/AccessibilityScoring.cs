namespace Kinetic;

/// <summary>Provides the application's heuristic accessibility scoring and impact ordering.</summary>
/// <remarks>
/// These scores help prioritize pages; they are not a WCAG compliance measurement.
/// A rule counts once per page regardless of how many elements it affects.
/// </remarks>
public static class AccessibilityScoring
{
    /// <summary>Returns the display-order weight associated with an axe-core impact level.</summary>
    /// <param name="impact">The impact label reported by axe-core.</param>
    /// <returns>A higher value indicates a more severe impact; unknown levels use the minor weight.</returns>
    /// <remarks>This weight is used to order findings, not to calculate the page score.</remarks>
    public static int ImpactWeight(string? impact) => impact?.ToLowerInvariant() switch
    {
        "critical" => 10,
        "serious" => 5,
        "moderate" => 3,
        "minor" => 1,
        _ => 1
    };

    /// <summary>
    /// Calculates a 0–100 score by subtracting five points for each distinct issue type.
    /// </summary>
    /// <param name="issueTypes">The identifiers of detected issue types.</param>
    /// <returns>The heuristic score, from 0 to 100.</returns>
    /// <remarks>
    /// Repeated occurrences of the same rule count once. The count is capped at twenty,
    /// so twenty or more distinct issue types result in a score of zero.
    /// </remarks>
    public static int Calculate(IEnumerable<string> issueTypes)
    {
        // Counting distinct rules avoids lowering a score simply because one issue appears on many elements.
        var count = issueTypes.Distinct(StringComparer.Ordinal).Take(20).Count();
        return 100 - 5 * count;
    }

    /// <summary>Maps a score to the label used to summarize its quality band.</summary>
    /// <param name="score">The score to classify, or null when no score is available.</param>
    /// <returns><c>none</c>, <c>good</c>, <c>fair</c>, or <c>poor</c>.</returns>
    /// <remarks>Scores of 90 or higher are good, 50–89 are fair, and scores below 50 are poor.</remarks>
    public static string Band(int? score) => score switch
    {
        null => "none",
        >= 90 => "good",
        >= 50 => "fair",
        _ => "poor"
    };
}
