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

    /// <summary>
    /// Score = 100 * e^(-penalty / decay), where penalty is the sum of impact weight times affected node count.
    /// </summary>
    public static int Calculate(IEnumerable<(string? Impact, int NodeCount)> violations, double decayConstant)
    {
        var penalty = violations.Sum(v => ImpactWeight(v.Impact) * Math.Max(1, v.NodeCount));
        var decay = decayConstant > 0 ? decayConstant : 50;
        return (int)Math.Round(100 * Math.Exp(-penalty / decay));
    }

    public static string Band(int? score) => score switch
    {
        null => "none",
        >= 90 => "good",
        >= 50 => "fair",
        _ => "poor"
    };
}
