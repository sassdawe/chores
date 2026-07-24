using System.Text.Json;

namespace Chores.Services;

/// <summary>
/// Singleton service that loads, caches, and provides access to all translation dictionaries.
/// Built-in translations are loaded from the app's Localization/ directory.
/// Admins can add or override language files by placing JSON files in {DataDirectory}/languages/.
/// Enabled languages are configured via the Localization:EnabledLanguages setting.
/// </summary>
public class TranslationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip };

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _translations;

    public TranslationStore(IConfiguration configuration, IWebHostEnvironment env)
    {
        var builtInDir = Path.Combine(env.ContentRootPath, "Localization");
        var builtIn = LoadDirectory(builtInDir);

        var dataDir = configuration["DataDirectory"]
            ?? Path.Combine(env.ContentRootPath, "data");
        var uploadedDir = Path.Combine(dataDir, "languages");
        var uploaded = Directory.Exists(uploadedDir)
            ? LoadDirectory(uploadedDir)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Merge: uploaded entries override built-in entries for the same key
        var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, entries) in builtIn)
        {
            var final = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);
            if (uploaded.TryGetValue(code, out var uploadedEntries))
            {
                foreach (var (key, val) in uploadedEntries)
                    final[key] = val;
            }
            merged[code] = final;
        }

        // Add uploaded-only languages (not present in built-in files)
        foreach (var (code, entries) in uploaded)
        {
            if (!merged.ContainsKey(code))
                merged[code] = entries;
        }

        _translations = merged;

        var enabledRaw = configuration["Localization:EnabledLanguages"] ?? "en";
        EnabledLanguages = [.. enabledRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// The list of language codes that administrators have enabled via configuration.
    /// </summary>
    public IReadOnlyList<string> EnabledLanguages { get; }

    /// <summary>Returns the translation dictionary for the given culture code, or null if not found.</summary>
    public IReadOnlyDictionary<string, string>? GetTranslations(string culture)
    {
        _translations.TryGetValue(culture, out var dict);
        return dict;
    }

    /// <summary>Returns the human-readable display name of a language (from its _name key).</summary>
    public string GetLanguageName(string culture)
    {
        if (_translations.TryGetValue(culture, out var dict) && dict.TryGetValue("_name", out var name))
            return name;
        return culture;
    }

    /// <summary>Returns true if the given culture code is in the enabled languages list.</summary>
    public bool IsEnabled(string culture)
        => EnabledLanguages.Any(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, Dictionary<string, string>> LoadDirectory(string dir)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir))
            return result;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var text = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(text, JsonOptions);
                if (dict is not null && dict.TryGetValue("_code", out var code))
                    result[code] = dict;
            }
            catch
            {
                // Skip malformed files silently
            }
        }

        return result;
    }
}
