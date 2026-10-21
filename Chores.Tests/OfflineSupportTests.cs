using System.Text.Json;

namespace Chores.Tests;

/// <summary>
/// Guards the wiring the offline PWA depends on. These are plain file assertions because the
/// behaviour lives in static assets that are never exercised by the server-side test host.
/// </summary>
public class OfflineSupportTests
{
    private static readonly string[] OfflineTranslationKeys =
    [
        "offline.banner",
        "offline.pending",
        "offline.syncing",
        "offline.synced",
        "offline.pendingBadge",
        "offline.readOnly"
    ];

    [Theory]
    [InlineData("en.json")]
    [InlineData("de.json")]
    [InlineData("es.json")]
    [InlineData("hu.json")]
    public void EveryLanguage_DefinesOfflineMessages(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AppFile("Localization", fileName)));

        foreach (var key in OfflineTranslationKeys)
        {
            Assert.True(
                document.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()),
                $"{fileName} is missing a translation for '{key}'.");
        }
    }

    [Fact]
    public void ServiceWorker_CachesTheOfflineQueueScripts()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("importScripts('./js/offline-queue.js')", serviceWorker);
        Assert.Contains("./js/offline-sync.js", serviceWorker);
    }

    [Fact]
    public void ServiceWorker_FallsBackToCachedPagesWhenNavigationFails()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("handleNavigation", serviceWorker);
        Assert.Contains("caches.match(request)", serviceWorker);
        Assert.Contains("caches.match(dashboardUrl)", serviceWorker);
        Assert.Contains("caches.match(offlineUrl)", serviceWorker);
    }

    [Fact]
    public void ServiceWorker_NeverCachesTheSyncApi()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("url.pathname.startsWith(scopePath + 'api/')", serviceWorker);
    }

    [Fact]
    public void ServiceWorker_DropsCachedPagesOnLogout()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("/Auth/Logout", serviceWorker);
        Assert.Contains("caches.delete(PAGE_CACHE)", serviceWorker);
    }

    [Fact]
    public void ServiceWorker_RegistersBackgroundSyncHandler()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("addEventListener('sync'", serviceWorker);
        Assert.Contains("ChoresOfflineQueue.flush()", serviceWorker);
    }

    [Fact]
    public void ServiceWorker_WarmsChoreConfirmationPagesOnRequest()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));

        Assert.Contains("chores-precache", serviceWorker);
        Assert.Contains("chores-precache", syncScript);
        Assert.Contains("a[href*=\"Chores/Complete\"]", syncScript);
    }

    [Fact]
    public void Precache_CoversTheHistoryAndEditPagesReachedFromTheChoreList()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));

        Assert.Contains("a[href*=\"Chores/History\"]", syncScript);
        Assert.Contains("a[href*=\"Chores/Edit\"]", syncScript);
    }

    [Fact]
    public void Precache_BudgetIsConfigurablePerDeployment()
    {
        var layout = File.ReadAllText(AppFile("Pages", "Shared", "_Layout.cshtml"));
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("Offline:MaxPrecachedPages", layout);
        Assert.Contains("chores-max-precached-pages", layout);
        Assert.Contains("chores-max-precached-pages", syncScript);
        Assert.Contains("limit: limit", syncScript);

        // An unset or unusable value leaves the worker on its own default.
        Assert.Contains("Number.isInteger(limit) && limit >= 0 ? limit : DEFAULT_MAX_PRECACHED_PAGES", serviceWorker);
    }

    [Fact]
    public void Precache_CanBeDisabledEntirely()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.Contains("if (limit === 0) return;", syncScript);
        Assert.Contains("if (budget === 0) return;", serviceWorker);
    }

    [Fact]
    public void Precache_DefaultBudgetIsShipped()
    {
        var settings = File.ReadAllText(AppFile("appsettings.json"));

        using var document = JsonDocument.Parse(settings);
        var limit = document.RootElement.GetProperty("Offline").GetProperty("MaxPrecachedPages").GetInt32();

        Assert.True(limit > 0);
    }

    [Fact]
    public void OfflineNavigation_OnlySubstitutesTheDashboardForTheDashboard()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        // An uncached page must land on the offline notice rather than silently showing the
        // dashboard, which reads as an unexplained redirect.
        Assert.Contains("if (new URL(request.url).pathname === dashboardUrl) {", serviceWorker);
        Assert.Contains("return await caches.match(offlineUrl) || Response.error();", serviceWorker);
    }

    [Fact]
    public void OfflineQueue_StoresCompletionsKeyedByClientRecordId()
    {
        var queueScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-queue.js"));

        Assert.Contains("createObjectStore(STORE_NAME, { keyPath: 'clientRecordId' })", queueScript);
        Assert.Contains("api/completions", queueScript);
        Assert.Contains("api/antiforgery/token", queueScript);
    }

    [Fact]
    public void OfflineQueue_BatchLimitMatchesTheServerLimit()
    {
        var queueScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-queue.js"));

        Assert.Contains($"var MAX_BATCH_SIZE = {Chores.Services.ChoreCompletionService.MaxBatchSize};", queueScript);
    }

    [Fact]
    public void Pages_DelegateFlushingToTheServiceWorker()
    {
        var queueScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-queue.js"));
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        // Only one context may post the queue, otherwise two flushes collide on the unique index.
        Assert.Contains("function flushShared()", queueScript);
        Assert.Contains("flushViaController", queueScript);
        Assert.DoesNotContain("queue.flush()", syncScript);
        Assert.Contains("queue.flushShared()", syncScript);
        Assert.Contains("replyPort.postMessage({ result: result })", serviceWorker);
    }

    [Fact]
    public void Precache_CoversTheCurrentPageAndItsReturnPath()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));

        // The first view of a session loads before the worker claims the page, so that navigation
        // never reaches handleNavigation and has to be warmed explicitly.
        Assert.Contains("urls.push(location.href);", syncScript);
        Assert.Contains("data-return-path", syncScript);
    }

    [Fact]
    public void Precache_CoversTheNavigationTargets()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));

        Assert.Contains(".navbar-brand", syncScript);
        Assert.Contains(".navbar-nav a.nav-link", syncScript);
    }

    [Fact]
    public void Precache_RanksCompletionPagesAboveReferencePages()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        var completion = syncScript.IndexOf("a[href*=\"Chores/Complete\"]", StringComparison.Ordinal);
        var navigation = syncScript.IndexOf(".navbar-nav a.nav-link", StringComparison.Ordinal);
        var reference = syncScript.IndexOf("a[href*=\"Chores/History\"]", StringComparison.Ordinal);

        // Completing a chore is the only thing that still works offline, so a long chore list has
        // to crowd out the read-only pages rather than the confirmation pages.
        Assert.True(completion > 0 && navigation > completion && reference > navigation);

        // The worker honours that order by spending its budget from the top.
        Assert.Contains("const unique = [...new Set(urls)];", serviceWorker);
        Assert.Contains("if (fetched >= budget) break;", serviceWorker);
    }

    [Fact]
    public void Precache_ResumesAcrossPageLoadsSoLongChoreListsFillIn()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        // The budget caps fetches per load, not total coverage, so a chore list bigger than the
        // budget is still fully cached after a few visits.
        Assert.Contains("if (await isWarm(cache, absolute)) continue;", serviceWorker);
        Assert.Contains("PRECACHE_REFRESH_MS", serviceWorker);
    }

    [Fact]
    public void ManagementPages_AreMarkedReadOnlyForOfflineUse()
    {
        var layout = File.ReadAllText(AppFile("Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("data-offline-readonly", layout);
        Assert.Contains("data-message-read-only", layout);
        // Completing and reviewing a chore must stay usable offline.
        Assert.Contains("\"/Chores/Complete\", \"/Chores/History\"", layout);
    }

    [Fact]
    public void ReadOnlyPages_DisableTheirSubmitControlsWhileOffline()
    {
        var syncScript = File.ReadAllText(AppFile("wwwroot", "js", "offline-sync.js"));

        Assert.Contains("[data-offline-readonly]", syncScript);
        Assert.Contains("messages.readOnly", syncScript);
        // Only controls the script disabled itself may be re-enabled when the network returns.
        Assert.Contains("data-offline-disabled", syncScript);
    }

    [Fact]
    public void OfflineNavigation_DoesNotServeAnotherChoresConfirmationPage()
    {
        var serviceWorker = File.ReadAllText(AppFile("wwwroot", "service-worker.js"));

        Assert.DoesNotContain("caches.match(request, { ignoreSearch: true })", serviceWorker);
        Assert.Contains("await caches.match(request)", serviceWorker);
        // A filtered dashboard has to seed the plain one, which is where the fallback lands.
        Assert.Contains("cache.put(dashboardUrl, response)", serviceWorker);
    }

    [Fact]
    public void Layout_LoadsTheOfflineScriptsAndExposesTheBasePath()
    {
        var layout = File.ReadAllText(AppFile("Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("name=\"chores-base-path\"", layout);
        Assert.Contains("~/js/offline-queue.js", layout);
        Assert.Contains("~/js/offline-sync.js", layout);
        Assert.Contains("id=\"choresOfflineStatus\"", layout);
    }

    [Fact]
    public void CompletePage_ExposesTheDataTheOfflineQueueNeeds()
    {
        var markup = File.ReadAllText(AppFile("Pages", "Chores", "Complete.cshtml"));

        Assert.Contains("data-offline-complete", markup);
        Assert.Contains("data-return-path=\"@Model.BuildDashboardPath()\"", markup);
        Assert.Contains("data-offline-mode=\"yesterday\"", markup);
        Assert.Contains("data-offline-mode=\"skip\"", markup);
        Assert.Contains("data-offline-mode=\"custom\"", markup);
    }

    [Fact]
    public void Dashboard_TagsChoreEntriesWithTheirId()
    {
        var markup = File.ReadAllText(AppFile("Pages", "Index.cshtml"));

        Assert.Contains("class=\"chore-name\" data-chore-id=\"@cs.Chore.Id\"", markup);
        Assert.Contains("class=\"agenda-entry-main\" data-chore-id=\"@entry.Chore.Id\"", markup);
    }

    private static string AppFile(params string[] segments)
    {
        var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", "..", "Chores" };
        parts.AddRange(segments);
        return Path.GetFullPath(Path.Combine([.. parts]));
    }
}
