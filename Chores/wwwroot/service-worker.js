importScripts('./js/offline-queue.js');

const VERSION = 'v3';
const SHELL_CACHE = `chores-shell-${VERSION}`;
const PAGE_CACHE = `chores-pages-${VERSION}`;
const CURRENT_CACHES = [SHELL_CACHE, PAGE_CACHE];

// Keeps the cache warm-up bounded on households with a long chore list. Deployments override it
// with Offline:MaxPrecachedPages, which the page passes along with each warm-up request.
const DEFAULT_MAX_PRECACHED_PAGES = 60;

// How long a warmed page counts as current before the warm-up spends budget on it again.
const PRECACHE_REFRESH_MS = 24 * 60 * 60 * 1000;

const APP_SHELL = [
  './offline.html',
  './manifest.json',
  './favicon.ico',
  './lib/bootstrap/dist/css/bootstrap.min.css',
  './lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  './lib/jquery/dist/jquery.min.js',
  './js/offline-queue.js',
  './js/offline-sync.js',
  './icons/icon-192.png',
  './icons/icon-512.png'
];

const scopePath = new URL(self.registration.scope).pathname;
const offlineUrl = scopePath + 'offline.html';
const dashboardUrl = scopePath;

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(SHELL_CACHE)
      .then(cache => Promise.allSettled(APP_SHELL.map(url => cache.add(url))))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(names => Promise.all(names
        .filter(name => !CURRENT_CACHES.includes(name))
        .map(name => caches.delete(name))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  if (url.origin !== self.location.origin) {
    return;
  }

  // Signing out must not leave the previous user's chore pages readable in the cache.
  if (request.method === 'POST' && url.pathname.endsWith('/Auth/Logout')) {
    event.respondWith(handleLogout(request));
    return;
  }

  if (request.method !== 'GET') {
    return;
  }

  if (url.pathname === scopePath + 'service-worker.js' || url.pathname.startsWith(scopePath + 'api/')) {
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(handleNavigation(event, request));
    return;
  }

  if (!isStaticAsset(request, url)) {
    return;
  }

  event.respondWith(
    (async () => {
      try {
        const response = await fetch(request);

        if (response.ok && response.type === 'basic') {
          const copy = response.clone();
          event.waitUntil(caches.open(SHELL_CACHE).then(cache => cache.put(request, copy)));
        }

        return response;
      } catch {
        const cached = await caches.match(request);
        return cached || Response.error();
      }
    })()
  );
});

// Network-first: an online device always sees live chore data, and the last successful render is
// kept so the same page still opens once the device drops off the network.
async function handleNavigation(event, request) {
  try {
    const response = await fetch(request);

    if (isCacheableNavigation(request, response)) {
      event.waitUntil(cacheNavigation(request, response.clone()));
    }

    return response;
  } catch {
    // Exact match only: ignoring the query string here would answer "complete chore 14" with a
    // cached confirmation page for whichever chore happened to be stored first.
    const cached = await caches.match(request);
    if (cached) return cached;

    // Only a differently filtered dashboard may be answered with the plain one. Handing it to any
    // other request looks like the app silently navigated somewhere else instead of failing.
    if (new URL(request.url).pathname === dashboardUrl) {
      const dashboard = await caches.match(dashboardUrl);
      if (dashboard) return dashboard;
    }

    return await caches.match(offlineUrl) || Response.error();
  }
}

async function cacheNavigation(request, response) {
  const cache = await caches.open(PAGE_CACHE);
  await cache.put(request, response.clone());

  // A filtered or sorted dashboard also stands in for the plain one, which is where every offline
  // fallback lands.
  if (new URL(request.url).pathname === dashboardUrl) {
    await cache.put(dashboardUrl, response);
  }
}

function isCacheableNavigation(request, response) {
  if (!response.ok || response.type !== 'basic' || response.redirected) return false;

  // Sign-in and sign-out responses are session specific and worthless offline.
  if (new URL(request.url).pathname.includes('/Auth/')) return false;

  return !(response.headers.get('Cache-Control') || '').includes('no-store');
}

async function handleLogout(request) {
  try {
    return await fetch(request);
  } finally {
    await caches.delete(PAGE_CACHE);
  }
}

function isStaticAsset(request, url) {
  return ['style', 'script', 'image', 'font', 'manifest'].includes(request.destination)
    || url.pathname === scopePath + 'favicon.ico'
    || url.pathname.startsWith(scopePath + 'icons/');
}

// Background Sync (Chromium). Browsers without it fall back to the page-level 'online' handler.
self.addEventListener('sync', event => {
  if (event.tag !== self.ChoresOfflineQueue.SYNC_TAG) return;

  event.waitUntil(
    self.ChoresOfflineQueue.flush().then(result => {
      if (result && result.synced > 0) {
        return notifyClients(result.synced);
      }
      // Rejecting lets the browser retry the sync later using its own backoff.
      if (result && result.remaining !== 0) {
        throw new Error('Completions still pending');
      }
    })
  );
});

self.addEventListener('message', event => {
  const data = event.data;
  if (!data) return;

  if (data.type === 'chores-flush-completions') {
    const replyPort = event.ports && event.ports[0];
    event.waitUntil(self.ChoresOfflineQueue.flush().then(result => {
      // The page waits for this reply instead of flushing itself, so the worker stays the only
      // context posting the queue and two flushes can't race over the same completion.
      if (replyPort) replyPort.postMessage({ result: result });
      if (result && result.synced > 0) return notifyClients(result.synced);
    }));
    return;
  }

  // The dashboard asks us to warm the confirmation pages it links to, so a chore can still be
  // marked done offline even if the user never opened that chore while connected.
  if (data.type === 'chores-precache' && Array.isArray(data.urls)) {
    event.waitUntil(precachePages(data.urls, data.limit));
  }
});

async function precachePages(urls, limit) {
  const budget = Number.isInteger(limit) && limit >= 0 ? limit : DEFAULT_MAX_PRECACHED_PAGES;
  if (budget === 0) return;

  const cache = await caches.open(PAGE_CACHE);
  // The page orders its URLs most-important first and Set preserves that, so a budget too small
  // for the whole list is spent on the pages it ranked highest.
  const unique = [...new Set(urls)];
  let fetched = 0;

  for (const url of unique) {
    // The budget counts fetches rather than pages, so each visit resumes where the last one ran
    // out and a chore list larger than the budget still fills in over a few page loads.
    if (fetched >= budget) break;

    const absolute = new URL(url, self.location.origin);
    if (absolute.origin !== self.location.origin) continue;
    if (await isWarm(cache, absolute)) continue;

    try {
      const response = await fetch(absolute, { credentials: 'same-origin' });
      fetched++;

      if (response.ok && !response.redirected) {
        await cache.put(absolute.toString(), response.clone());

        if (absolute.pathname === dashboardUrl) {
          await cache.put(dashboardUrl, response);
        }
      }
    } catch {
      // Lost the network mid-warm; whatever was already cached still stands.
      break;
    }
  }
}

// A warmed page is left alone until it ages out, so repeat visits spend the budget on pages that
// are not cached yet. Pages the user actually opens are refreshed by the network-first navigation.
async function isWarm(cache, url) {
  const cached = await cache.match(url.toString());
  if (!cached) return false;

  const cachedAt = Date.parse(cached.headers.get('date') || '');
  return Number.isFinite(cachedAt) && Date.now() - cachedAt < PRECACHE_REFRESH_MS;
}

async function notifyClients(synced) {
  const clients = await self.clients.matchAll({ type: 'window' });
  clients.forEach(client => client.postMessage({ type: 'chores-completions-synced', synced: synced }));
}

