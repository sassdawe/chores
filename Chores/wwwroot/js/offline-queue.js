// Offline completion queue, shared by the page scripts and the service worker.
// Loaded in the page with a <script> tag and in the worker with importScripts(), so it must not
// touch anything that only exists in one of the two contexts.
(function (scope) {
    'use strict';

    var DB_NAME = 'chores-offline';
    var DB_VERSION = 1;
    var STORE_NAME = 'pendingCompletions';
    var MAX_BATCH_SIZE = 200;
    var SYNC_TAG = 'chores-sync-completions';

    // How long a page waits for the service worker's flush answer before doing it itself.
    var CONTROLLER_FLUSH_TIMEOUT_MS = 15000;

    var flushInFlight = null;

    function openDb() {
        return new Promise(function (resolve, reject) {
            var request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = function () {
                var db = request.result;
                if (!db.objectStoreNames.contains(STORE_NAME)) {
                    db.createObjectStore(STORE_NAME, { keyPath: 'clientRecordId' });
                }
            };
            request.onsuccess = function () { resolve(request.result); };
            request.onerror = function () { reject(request.error); };
            request.onblocked = function () { reject(new Error('IndexedDB upgrade blocked')); };
        });
    }

    function withStore(mode, work) {
        return openDb().then(function (db) {
            return new Promise(function (resolve, reject) {
                var transaction = db.transaction(STORE_NAME, mode);
                var result = work(transaction.objectStore(STORE_NAME));
                transaction.oncomplete = function () { db.close(); resolve(result && result.value); };
                transaction.onerror = function () { db.close(); reject(transaction.error); };
                transaction.onabort = function () { db.close(); reject(transaction.error); };
            });
        });
    }

    function request(store, call) {
        var holder = {};
        var req = call(store);
        req.onsuccess = function () { holder.value = req.result; };
        return holder;
    }

    function basePath() {
        if (scope.registration && scope.registration.scope) {
            return new URL(scope.registration.scope).pathname;
        }

        var meta = scope.document && scope.document.querySelector('meta[name="chores-base-path"]');
        var value = (meta && meta.getAttribute('content')) || '';
        return value.charAt(value.length - 1) === '/' ? value : value + '/';
    }

    function endpoint(relativePath) {
        return basePath() + relativePath;
    }

    function add(entry) {
        var record = {
            clientRecordId: entry.clientRecordId || generateId(),
            choreId: entry.choreId,
            choreName: entry.choreName || '',
            completedAtUtc: entry.completedAtUtc,
            isSkipped: entry.isSkipped === true,
            queuedAtUtc: new Date().toISOString()
        };

        return withStore('readwrite', function (store) { store.put(record); })
            .then(function () { return record; });
    }

    function all() {
        return withStore('readonly', function (store) {
            return request(store, function (s) { return s.getAll(); });
        }).then(function (records) { return records || []; });
    }

    function count() {
        return withStore('readonly', function (store) {
            return request(store, function (s) { return s.count(); });
        }).then(function (value) { return value || 0; });
    }

    function remove(ids) {
        if (!ids || !ids.length) return Promise.resolve();
        return withStore('readwrite', function (store) {
            ids.forEach(function (id) { store.delete(id); });
        });
    }

    function clear() {
        return withStore('readwrite', function (store) { store.clear(); });
    }

    function generateId() {
        if (scope.crypto && typeof scope.crypto.randomUUID === 'function') {
            return scope.crypto.randomUUID();
        }

        var bytes = new Uint8Array(16);
        scope.crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        var hex = Array.prototype.map.call(bytes, function (b) {
            return ('0' + b.toString(16)).slice(-2);
        }).join('');
        return hex.slice(0, 8) + '-' + hex.slice(8, 12) + '-' + hex.slice(12, 16) + '-' + hex.slice(16, 20) + '-' + hex.slice(20);
    }

    function fetchAntiforgeryToken() {
        return fetch(endpoint('api/antiforgery/token'), {
            method: 'GET',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        }).then(function (response) {
            if (!response.ok) return null;
            return response.json();
        }).catch(function () { return null; });
    }

    function sendBatch(batch, token) {
        var headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        headers[token.headerName || 'RequestVerificationToken'] = token.token;

        return fetch(endpoint('api/completions'), {
            method: 'POST',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: headers,
            body: JSON.stringify({
                completions: batch.map(function (item) {
                    return {
                        clientRecordId: item.clientRecordId,
                        choreId: item.choreId,
                        completedAtUtc: item.completedAtUtc,
                        isSkipped: item.isSkipped === true
                    };
                })
            })
        });
    }

    // Resolves with { synced, remaining, offline }. Never rejects: a failed flush simply leaves the
    // queue intact so it can be retried on the next online event or background sync.
    function flush() {
        if (flushInFlight) return flushInFlight;

        flushInFlight = runFlush().then(function (result) {
            flushInFlight = null;
            return result;
        }, function () {
            flushInFlight = null;
            return { synced: 0, remaining: -1, offline: true };
        });

        return flushInFlight;
    }

    function runFlush() {
        if (typeof navigator !== 'undefined' && navigator.onLine === false) {
            return count().then(function (remaining) {
                return { synced: 0, remaining: remaining, offline: true };
            });
        }

        return all().then(function (pending) {
            if (!pending.length) return { synced: 0, remaining: 0, offline: false };

            return fetchAntiforgeryToken().then(function (token) {
                if (!token || !token.token) {
                    return { synced: 0, remaining: pending.length, offline: true };
                }

                var batch = pending.slice(0, MAX_BATCH_SIZE);
                return sendBatch(batch, token).then(function (response) {
                    if (!response.ok) {
                        return { synced: 0, remaining: pending.length, offline: true };
                    }
                    return response.json();
                }).then(function (payload) {
                    if (!payload || !payload.results) return payload;

                    // "recorded", "duplicate" and "rejected" are all final answers, so the entries
                    // are dropped; anything the server could not answer stays queued.
                    var settled = payload.results.map(function (r) { return r.clientRecordId; });
                    var synced = payload.results.filter(function (r) { return r.status === 'recorded'; }).length;

                    return remove(settled)
                        .then(count)
                        .then(function (remaining) {
                            return { synced: synced, remaining: remaining, offline: false };
                        });
                });
            });
        });
    }

    function requestBackgroundSync(registration) {
        if (!registration || !registration.sync) return Promise.resolve(false);
        return registration.sync.register(SYNC_TAG)
            .then(function () { return true; })
            .catch(function () { return false; });
    }

    // Asks the controlling service worker to do the flush and waits for its answer. The worker is
    // shared by every tab, so routing through it keeps exactly one flush in flight per device --
    // two contexts posting the same queued completion would collide on the server's unique index.
    function flushViaController() {
        var serviceWorker = scope.navigator && scope.navigator.serviceWorker;
        var controller = serviceWorker && serviceWorker.controller;
        if (!controller || typeof MessageChannel === 'undefined') return null;

        return new Promise(function (resolve) {
            var channel = new MessageChannel();
            var settled = false;

            function settle(value) {
                if (settled) return;
                settled = true;
                clearTimeout(timer);
                channel.port1.close();
                resolve(value);
            }

            var timer = setTimeout(function () { settle(null); }, CONTROLLER_FLUSH_TIMEOUT_MS);
            channel.port1.onmessage = function (event) {
                settle((event.data && event.data.result) || null);
            };

            try {
                controller.postMessage({ type: 'chores-flush-completions' }, [channel.port2]);
            } catch {
                settle(null);
            }
        });
    }

    // Preferred entry point for pages: delegates to the service worker when there is one, and only
    // flushes locally when the worker is absent or silent.
    function flushShared() {
        var delegated = flushViaController();
        if (!delegated) return flush();

        return delegated.then(function (result) { return result || flush(); });
    }

    scope.ChoresOfflineQueue = {
        SYNC_TAG: SYNC_TAG,
        add: add,
        all: all,
        count: count,
        remove: remove,
        clear: clear,
        flush: flush,
        flushShared: flushShared,
        requestBackgroundSync: requestBackgroundSync
    };
})(self);
