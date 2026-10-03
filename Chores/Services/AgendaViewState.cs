using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Chores.Services;

/// <summary>
/// Open/closed state of the agenda's collapsible blocks, stored in a per-user session cookie.
/// Only blocks the user actually toggled are stored; everything else falls back to the view default.
/// The cookie is written by the browser, so it cannot be HttpOnly; it holds no sensitive data.
/// </summary>
public static class AgendaViewState
{
    public const string CookiePrefix = "Chores.AgendaState.";
    public const int MaxCookieLength = 3000;
    public const string OverdueKey = "overdue";
    public const string UnscheduledKey = "adhoc";

    private const char EntrySeparator = '|';
    private const char ValueSeparator = ':';

    /// <summary>Scopes the cookie to one login name without revealing it to anyone reading the browser's cookie jar.</summary>
    public static string CookieName(string? loginName)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(loginName ?? string.Empty));
        return CookiePrefix + Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    public static string DayKey(DateTime dateUtc) =>
        "d" + dateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static IReadOnlyDictionary<string, bool> Parse(string? cookieValue)
    {
        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(cookieValue) || cookieValue.Length > MaxCookieLength)
        {
            return states;
        }

        foreach (var token in cookieValue.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 3 || token[^2] != ValueSeparator)
            {
                continue;
            }

            var flag = token[^1];
            if (flag is not ('0' or '1'))
            {
                continue;
            }

            states[token[..^2]] = flag == '1';
        }

        return states;
    }
}
