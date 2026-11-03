namespace Kinetic;

public static class AccessibilityScoring
{
    public static int ImpactWeight(string? impact) => impact?.ToLowerInvariant() switch
    {
        "critical" => 10,
        "serious" => 5,
        "moderate" => 3,
        "minor" => 1,
        _ => 1
    };

    public static int Calculate(IEnumerable<string> issueTypes)
    {
        var count = issueTypes.Distinct(StringComparer.Ordinal).Take(20).Count();
        return 100 - 5 * count;
    }

    public static string Band(int? score) => score switch
    {
        null => "none",
        >= 90 => "good",
        >= 50 => "fair",
        _ => "poor"
    };
}
