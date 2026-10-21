// Page-level glue for the offline completion queue: captures completions while the device is
// offline, replays them when it comes back, and keeps the UI honest about what is still pending.
(function () {
    'use strict';

    var queue = self.ChoresOfflineQueue;
    if (!queue || typeof indexedDB === 'undefined') return;

    var statusElement = document.getElementById('choresOfflineStatus');
    var readOnlyRegion = document.querySelector('[data-offline-readonly]');
    var messages = readMessages(statusElement);
    var syncedNoticeTimer = null;

    function readMessages(element) {
        function read(name, fallback) {
            var value = element && element.getAttribute(name);
            return value || fallback;
        }

        return {
            offline: read('data-message-offline', 'You are offline. Completions are saved on this device.'),
            pending: read('data-message-pending', '{0} waiting to sync'),
            syncing: read('data-message-syncing', 'Syncing…'),
            synced: read('data-message-synced', 'Synced {0} completion(s).'),
            pendingBadge: read('data-message-pending-badge', 'Pending sync'),
            readOnly: read('data-message-read-only', 'You are offline. This page is read-only.')
        };
    }

    function format(template, value) {
        return template.replace('{0}', value);
    }

    function showStatus(text, variant) {
        if (!statusElement) return;
        statusElement.textContent = text;
        statusElement.className = 'chores-offline-status alert alert-' + variant + ' py-2 px-3 mb-3';
    }

    function hideStatus() {
        if (!statusElement) return;
        statusElement.textContent = '';
        statusElement.className = 'chores-offline-status d-none';
    }

    function renderStatus(pending, syncedJustNow) {
        if (navigator.onLine === false) {
            // Nothing on a management page can be queued, so say so instead of promising a sync.
            var offlineText = readOnlyRegion ? messages.readOnly : messages.offline;
            showStatus(pending > 0
                ? offlineText + ' ' + format(messages.pending, pending)
                : offlineText, 'warning');
            return;
        }

        if (pending > 0) {
            showStatus(messages.syncing + ' ' + format(messages.pending, pending), 'info');
            return;
        }

        if (syncedJustNow > 0) {
            showStatus(format(messages.synced, syncedJustNow), 'success');
            clearTimeout(syncedNoticeTimer);
            syncedNoticeTimer = setTimeout(hideStatus, 5000);
            return;
        }

        hideStatus();
    }

    // Submitting one of these forms offline would just dead-end on a network error page, so the
    // controls are held disabled until the connection is back.
    function applyReadOnlyState() {
        if (!readOnlyRegion) return;

        var offline = navigator.onLine === false;
        var submits = readOnlyRegion.querySelectorAll('button[type="submit"], input[type="submit"]');

        Array.prototype.forEach.call(submits, function (control) {
            if (offline) {
                if (control.disabled) return;
                control.disabled = true;
                control.setAttribute('data-offline-disabled', '');
            } else if (control.hasAttribute('data-offline-disabled')) {
                control.disabled = false;
                control.removeAttribute('data-offline-disabled');
            }
        });
    }

    function renderPendingBadges(pendingRecords) {
        var pendingChoreIds = {};
        pendingRecords.forEach(function (record) { pendingChoreIds[record.choreId] = true; });

        var rows = document.querySelectorAll('[data-chore-id]:not([data-offline-complete])');
        Array.prototype.forEach.call(rows, function (row) {
            var existing = row.querySelector('[data-offline-pending-badge]');
            var isPending = pendingChoreIds[row.getAttribute('data-chore-id')] === true;

            if (!isPending) {
                if (existing) existing.remove();
                return;
            }

            if (existing) return;

            var badge = document.createElement('span');
            badge.className = 'badge text-bg-info ms-1';
            badge.setAttribute('data-offline-pending-badge', '');
            badge.textContent = messages.pendingBadge;
            row.appendChild(badge);
        });
    }

    function refresh(syncedJustNow) {
        return queue.all().then(function (pending) {
            renderStatus(pending.length, syncedJustNow || 0);
            renderPendingBadges(pending);
            applyReadOnlyState();
            return pending.length;
        }).catch(function () { /* queue unreadable; leave the UI as-is */ });
    }

    function flushAndRefresh(reloadOnSync) {
        return queue.flushShared().then(function (result) {
            if (reloadOnSync && result && result.synced > 0) {
                reloadOnce();
                return;
            }
            return refresh(result ? result.synced : 0);
        });
    }

    var reloadRequested = false;

    function reloadOnce() {
        if (reloadRequested) return;
        reloadRequested = true;
        location.reload();
    }

    // ── Completion capture on the confirmation page ──────────────────────────

    function toUtcIso(value) {
        var parsed = new Date(value);
        return isNaN(parsed.getTime()) ? null : parsed.toISOString();
    }

    function resolveCompletedAt(form, container) {
        var mode = form.getAttribute('data-offline-mode');

        if (mode === 'yesterday') return new Date(Date.now() - 86400000).toISOString();
        if (mode === 'skip') return new Date().toISOString();

        var input = container.querySelector('input[name="CompletedAt"]');
        return input && input.value ? toUtcIso(input.value) : null;
    }

    function setupCompletionForms() {
        var container = document.querySelector('[data-offline-complete]');
        if (!container) return;

        var choreId = parseInt(container.getAttribute('data-chore-id'), 10);
        var returnPath = container.getAttribute('data-return-path');
        if (!choreId || !returnPath) return;

        var forms = container.querySelectorAll('form[data-offline-mode]');
        Array.prototype.forEach.call(forms, function (form) {
            form.addEventListener('submit', function (event) {
                // A confirmation dialog earlier in the chain may already have cancelled this.
                if (event.defaultPrevented) return;

                var completedAtUtc = resolveCompletedAt(form, container);
                if (!completedAtUtc) return;

                event.preventDefault();
                submitCompletion(form, {
                    choreId: choreId,
                    choreName: container.getAttribute('data-chore-name') || '',
                    completedAtUtc: completedAtUtc,
                    isSkipped: form.getAttribute('data-offline-mode') === 'skip'
                }, returnPath);
            });
        });
    }

    function submitCompletion(form, entry, returnPath) {
        setFormsBusy(true);

        // Persist first, send second: a completion that reaches IndexedDB can never be lost, even
        // if the browser is closed before the request goes out.
        queue.add(entry)
            .then(function () {
                return navigator.onLine === false ? registerBackgroundSync() : queue.flushShared();
            })
            .then(function () { location.assign(returnPath); })
            .catch(function () {
                // IndexedDB unavailable (private browsing, blocked storage): fall back to the
                // plain server-rendered post so the user is never silently blocked.
                setFormsBusy(false);
                form.submit();
            });
    }

    function registerBackgroundSync() {
        if (!navigator.serviceWorker) return Promise.resolve(false);
        return navigator.serviceWorker.ready
            .then(function (registration) { return queue.requestBackgroundSync(registration); })
            .catch(function () { return false; });
    }

    function setFormsBusy(busy) {
        var buttons = document.querySelectorAll('[data-offline-complete] button[type="submit"]');
        Array.prototype.forEach.call(buttons, function (button) { button.disabled = busy; });
    }

    // ── Wiring ──────────────────────────────────────────────────────────────

    // Null means the deployment left it unset, so the worker applies its own default.
    function readPrecacheLimit() {
        var meta = document.querySelector('meta[name="chores-max-precached-pages"]');
        if (!meta) return null;

        var limit = parseInt(meta.getAttribute('content'), 10);
        return isFinite(limit) && limit >= 0 ? limit : null;
    }

    // Warms the pages this one can navigate to, so they are readable after the network drops.
    // The list is a priority order -- the worker keeps the first `limit` entries -- so a long chore
    // list pushes reference pages out rather than crowding out the ones that still do something.
    function precacheReachablePages() {
        if (navigator.onLine === false) return;
        if (!navigator.serviceWorker || !navigator.serviceWorker.controller) return;

        var limit = readPrecacheLimit();
        if (limit === 0) return;

        var urls = [];

        function addMatching(selector) {
            var found = document.querySelectorAll(selector);
            Array.prototype.forEach.call(found, function (link) { urls.push(link.href); });
        }

        // 1. Bearings. The current page is here because the first view of a session loads before the
        //    worker claims it -- that navigation never reaches the worker and would otherwise be
        //    missing offline. The brand link is the way back to the dashboard from anywhere.
        urls.push(location.href);

        var container = document.querySelector('[data-offline-complete]');
        var returnPath = container && container.getAttribute('data-return-path');
        if (returnPath) urls.push(new URL(returnPath, location.href).href);

        addMatching('.navbar-brand');

        // 2. Marking a chore done is the only thing that still works offline, so every chore's
        //    confirmation page outranks any page that can merely be read.
        addMatching('a[href*="Chores/Complete"]');

        // 3. Management pages. Read-only offline, and a fixed handful regardless of chore count.
        addMatching('.navbar-nav a.nav-link');

        // 4. Reference pages, one pair per chore. First to be dropped once the budget runs out.
        addMatching('a[href*="Chores/History"], a[href*="Chores/Edit"]');

        navigator.serviceWorker.controller.postMessage({ type: 'chores-precache', urls: urls, limit: limit });
    }

    window.addEventListener('online', function () { flushAndRefresh(true); });
    window.addEventListener('offline', function () { refresh(0); });

    if (navigator.serviceWorker) {
        navigator.serviceWorker.addEventListener('message', function (event) {
            // The worker already did the syncing; reload so the page shows the server's own view.
            if (event.data && event.data.type === 'chores-completions-synced') {
                reloadOnce();
            }
        });
    }

    setupCompletionForms();
    refresh(0).then(function (pending) {
        if (pending > 0 && navigator.onLine !== false) {
            flushAndRefresh(true);
        }
    });

    if ('requestIdleCallback' in window) {
        requestIdleCallback(precacheReachablePages, { timeout: 5000 });
    } else {
        setTimeout(precacheReachablePages, 2000);
    }
})();
