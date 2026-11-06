namespace Kinetic_A11y.Web.Common.Extensions;

public static class StringExtensions
{
    public static string CapitalizeWord(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
