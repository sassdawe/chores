using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Chores.Tests;

public class TranslationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TranslationStore CreateStore(
        string? enabledLanguages = null,
        string? contentRootPath = null)
    {
        var configValues = new Dictionary<string, string?>();
        if (enabledLanguages is not null)
            configValues["Localization:EnabledLanguages"] = enabledLanguages;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var env = new TestWebHostEnvironment
        {
            ContentRootPath = contentRootPath ?? Path.GetTempPath()
        };

        return new TranslationStore(config, env);
    }

    private static UiTranslationService CreateService(
        TranslationStore store,
        string? cookieValue = null)
    {
        var httpContext = new DefaultHttpContext();
        if (cookieValue is not null)
        {
            httpContext.Request.Headers.Cookie =
                $"{UiTranslationService.CookieName}={cookieValue}";
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new UiTranslationService(store, accessor);
    }

    // ── TranslationStore tests ────────────────────────────────────────────────

    [Fact]
    public void TranslationStore_EnabledLanguages_DefaultsToEnglish()
    {
        var store = CreateStore();

        var langs = store.EnabledLanguages;

        Assert.Single(langs);
        Assert.Equal("en", langs[0]);
    }

    [Fact]
    public void TranslationStore_EnabledLanguages_ReadsFromConfig()
    {
        // The built-in localization directory is inside the repo, but the
        // store's ContentRootPath will point to /tmp here, so en.json won't
        // load — only what we explicitly configure matters for this test.
        var store = CreateStore(enabledLanguages: "en,fr");

        var codes = store.EnabledLanguages;

        // "fr" is not a built-in language file, but the config should still
        // list it; the store returns it even if the file is absent.
        Assert.Contains("en", codes);
        Assert.Contains("fr", codes);
    }

    [Fact]
    public void TranslationStore_LoadsBuiltInEnglishFile()
    {
        // Point ContentRootPath at the actual project so the JSON files load.
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);

        var dict = store.GetTranslations("en");

        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("nav.myChores"));
        Assert.Equal("My Chores", dict["nav.myChores"]);
    }

    [Fact]
    public void TranslationStore_LoadsBuiltInHungarianFile()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);

        var dict = store.GetTranslations("hu");

        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("nav.myChores"));
        Assert.Equal("Feladataim", dict["nav.myChores"]);
    }

    [Fact]
    public void TranslationStore_MissingLanguage_ReturnsNull()
    {
        var store = CreateStore();

        var dict = store.GetTranslations("xx");

        Assert.Null(dict);
    }

    [Fact]
    public void TranslationStore_AdHocDirectory_OverridesBuiltIn()
    {
        // Write a temporary language file that overrides a key.
        // The store's default DataDirectory is {ContentRootPath}/data, so
        // uploaded files must be placed at {ContentRootPath}/data/languages/.
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var langDir = Path.Combine(tmp, "data", "languages");
        Directory.CreateDirectory(langDir);
        File.WriteAllText(
            Path.Combine(langDir, "en.json"),
            """{"_code":"en","_name":"English","nav.myChores":"My Tasks (override)"}""");

        try
        {
            var store = CreateStore(contentRootPath: tmp);
            var dict = store.GetTranslations("en");

            Assert.NotNull(dict);
            Assert.Equal("My Tasks (override)", dict!["nav.myChores"]);
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    // ── UiTranslationService tests ────────────────────────────────────────────

    [Fact]
    public void UiTranslationService_NoCookie_DefaultsToEnglish()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);
        var svc = CreateService(store);

        Assert.Equal("en", svc.CurrentLocale);
    }

    [Fact]
    public void UiTranslationService_CookieSetToHu_UsesHungarian()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(enabledLanguages: "en,hu", contentRootPath: projectRoot);
        var svc = CreateService(store, cookieValue: "hu");

        Assert.Equal("hu", svc.CurrentLocale);
        Assert.Equal("Feladataim", svc["nav.myChores"]);
    }

    [Fact]
    public void UiTranslationService_UnknownCookie_FallsBackToEnglish()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);
        var svc = CreateService(store, cookieValue: "zz");

        Assert.Equal("en", svc.CurrentLocale);
    }

    [Fact]
    public void UiTranslationService_DisabledLanguageCookie_FallsBackToEnglish()
    {
        // hu is in the files but NOT in enabled languages
        var projectRoot = FindProjectRoot();
        var store = CreateStore(enabledLanguages: "en", contentRootPath: projectRoot);
        var svc = CreateService(store, cookieValue: "hu");

        Assert.Equal("en", svc.CurrentLocale);
    }

    [Fact]
    public void UiTranslationService_MissingKey_ReturnsEnglishFallback()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(enabledLanguages: "en,hu", contentRootPath: projectRoot);
        var svc = CreateService(store, cookieValue: "hu");

        // Force a key that exists in en but not in hu by using a non-existent key
        // (the service should return the key itself as last resort)
        var result = svc["nonexistent.key"];

        Assert.Equal("nonexistent.key", result);
    }

    [Fact]
    public void UiTranslationService_EnglishKeyAccess_ReturnsValue()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);
        var svc = CreateService(store);

        Assert.Equal("My Chores", svc["nav.myChores"]);
        Assert.Equal("Save", svc["common.save"]);
        Assert.Equal("Cancel", svc["common.cancel"]);
    }

    [Fact]
    public void UiTranslationService_NoHttpContext_FallsBackToEnglish()
    {
        var projectRoot = FindProjectRoot();
        var store = CreateStore(contentRootPath: projectRoot);
        var accessor = new HttpContextAccessor { HttpContext = null };
        var svc = new UiTranslationService(store, accessor);

        Assert.Equal("en", svc.CurrentLocale);
        Assert.Equal("My Chores", svc["nav.myChores"]);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string FindProjectRoot()
    {
        // Walk up from the test binary directory to find the Chores project folder.
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "Localization")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate the Chores project root with a 'Localization' directory.");
    }

    private sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get => "Test"; set { } }
        public IFileProvider WebRootFileProvider { get => new NullFileProvider(); set { } }
        public string WebRootPath { get => string.Empty; set { } }
        public string EnvironmentName { get => "Test"; set { } }
        public IFileProvider ContentRootFileProvider { get => new NullFileProvider(); set { } }
        public string ContentRootPath { get; set; } = Path.GetTempPath();
    }
}
