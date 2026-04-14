namespace SampleApp.Utils;

/// <summary>
/// Utility class with no dependency on OrderService whatsoever.
/// EXPECTED: NOT a fan-in.
/// </summary>
public class Helpers
{
    public static string FormatCurrency(decimal amount)
    {
        return $"${amount:F2}";
    }

    public static bool IsValidId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && id.Length > 3;
    }
}
