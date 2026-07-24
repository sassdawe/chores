namespace Chores.Services;

/// <summary>
/// Scoped service providing UI translations for the current HTTP request.
/// The active language is determined from the Chores.Language cookie (set when the user saves their preference).
/// Falls back to English when the requested language is not enabled or not found.
/// </summary>
public class UiTranslationService
{
    public const string CookieName = "Chores.Language";

    private readonly TranslationStore _store;
    private readonly IReadOnlyDictionary<string, string>? _translations;
    private readonly IReadOnlyDictionary<string, string>? _englishFallback;

    public string CurrentLocale { get; }

    public UiTranslationService(TranslationStore store, IHttpContextAccessor httpContextAccessor)
    {
        _store = store;

        var cookie = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        var requested = string.IsNullOrWhiteSpace(cookie) ? "en" : cookie.Trim();

        var requestedTranslations = store.IsEnabled(requested) ? store.GetTranslations(requested) : null;

        if (requestedTranslations is not null)
        {
            CurrentLocale = requested;
            _translations = requestedTranslations;
        }
        else
        {
            CurrentLocale = "en";
            _translations = store.GetTranslations("en");
        }

        if (!string.Equals(CurrentLocale, "en", StringComparison.OrdinalIgnoreCase))
            _englishFallback = store.GetTranslations("en");
    }

    /// <summary>
    /// Returns the translated string for the given key.
    /// Falls back to the English value, then to the raw key if neither is available.
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (_translations is not null && _translations.TryGetValue(key, out var val))
                return val;

            if (_englishFallback is not null && _englishFallback.TryGetValue(key, out var enVal))
                return enVal;

            return key;
        }
    }

    /// <summary>
    /// Returns the list of enabled languages as (code, displayName) pairs.
    /// Used to populate the language selector on the profile page.
    /// </summary>
    public IReadOnlyList<(string Code, string Name)> GetEnabledLanguages()
        => [.. _store.EnabledLanguages.Select(c => (c, _store.GetLanguageName(c)))];
}
