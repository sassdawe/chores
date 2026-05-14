using System.Globalization;

namespace Chores.Models;

public static class LabelColorExtensions
{
    private const string LightTextColor = "#ffffff";
    private const string DarkTextColor = "#111827";

    public static string GetAccessibleTextColor(this Label label) => GetAccessibleTextColor(label.Color);

    public static string GetAccessibleTextColor(this string backgroundColor)
    {
        if (!TryParseHexColor(backgroundColor, out var red, out var green, out var blue))
            return LightTextColor;

        var whiteContrast = GetContrastRatio(red, green, blue, 255, 255, 255);
        var darkContrast = GetContrastRatio(red, green, blue, 17, 24, 39);

        return darkContrast >= whiteContrast ? DarkTextColor : LightTextColor;
    }

    private static bool TryParseHexColor(string color, out byte red, out byte green, out byte blue)
    {
        red = 0;
        green = 0;
        blue = 0;

        if (string.IsNullOrWhiteSpace(color))
            return false;

        var normalized = color.Trim().TrimStart('#');
        if (normalized.Length == 3)
        {
            normalized = string.Concat(normalized.Select(channel => new string(channel, 2)));
        }

        if (normalized.Length != 6)
            return false;

        if (!byte.TryParse(normalized.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red))
            return false;

        if (!byte.TryParse(normalized.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green))
            return false;

        return byte.TryParse(normalized.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue);
    }

    private static double GetContrastRatio(byte backgroundRed, byte backgroundGreen, byte backgroundBlue, byte foregroundRed, byte foregroundGreen, byte foregroundBlue)
    {
        var backgroundLuminance = GetRelativeLuminance(backgroundRed, backgroundGreen, backgroundBlue);
        var foregroundLuminance = GetRelativeLuminance(foregroundRed, foregroundGreen, foregroundBlue);
        var lighter = Math.Max(backgroundLuminance, foregroundLuminance);
        var darker = Math.Min(backgroundLuminance, foregroundLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(byte red, byte green, byte blue)
    {
        return (0.2126 * ToLinear(red)) + (0.7152 * ToLinear(green)) + (0.0722 * ToLinear(blue));
    }

    private static double ToLinear(byte colorChannel)
    {
        var normalized = colorChannel / 255d;
        return normalized <= 0.04045
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}