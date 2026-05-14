using System.Text.RegularExpressions;

namespace Chores.Services;

public static partial class LoginNameValidator
{
    public static bool TryNormalize(string? loginName, out string normalizedLoginName)
    {
        normalizedLoginName = (loginName ?? string.Empty).Trim();
        return LoginNamePattern().IsMatch(normalizedLoginName);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex LoginNamePattern();
}

public static partial class LabelColorValidator
{
    public static bool TryNormalize(string? color, out string normalizedColor)
    {
        normalizedColor = (color ?? string.Empty).Trim();
        if (!HexColorPattern().IsMatch(normalizedColor))
        {
            return false;
        }

        normalizedColor = NormalizeHexColor(normalizedColor);
        return true;
    }

    private static string NormalizeHexColor(string color)
    {
        if (color.Length == 4)
        {
            return string.Concat(
                "#",
                char.ToUpperInvariant(color[1]), char.ToUpperInvariant(color[1]),
                char.ToUpperInvariant(color[2]), char.ToUpperInvariant(color[2]),
                char.ToUpperInvariant(color[3]), char.ToUpperInvariant(color[3]));
        }

        return string.Concat(
            "#",
            char.ToUpperInvariant(color[1]),
            char.ToUpperInvariant(color[2]),
            char.ToUpperInvariant(color[3]),
            char.ToUpperInvariant(color[4]),
            char.ToUpperInvariant(color[5]),
            char.ToUpperInvariant(color[6]));
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();
}
