// Offline support for the published app: caches the app's own files (listed with integrity hashes in
// service-worker-assets.js) so the game starts without network after the first visit. Same-origin only.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'officina-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.png$/, /\.ico$/, /\.webmanifest$/, /\.dat$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

// Works under any sub-path (e.g. https://user.github.io/repo/): resolve against the registration scope.
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
  const assetsRequests = self.assetsManifest.assets
    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
    .map(asset => new Request(new URL(asset.url, baseUrl), { integrity: asset.hash, cache: 'no-cache' }));
  const cache = await caches.open(cacheName);
  await cache.addAll(assetsRequests);
  self.skipWaiting();
}

async function onActivate(event) {
  const cacheKeys = await caches.keys();
  await Promise.all(cacheKeys
    .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
    .map(key => caches.delete(key)));
  await self.clients.claim();
}

async function onFetch(event) {
  let cachedResponse = null;
  const url = new URL(event.request.url);
  // The demo lives in its own sub-folder with its own service worker: never answer for it from this cache.
  if (url.pathname.startsWith(baseUrl.pathname + 'demo/') && !baseUrl.pathname.endsWith('/demo/')) return fetch(event.request);
  if (event.request.method === 'GET' && url.origin === self.location.origin) {
    const shouldServeIndexHtml = event.request.mode === 'navigate' && !manifestUrlList.some(u => u === event.request.url);
    const request = shouldServeIndexHtml ? new URL('index.html', baseUrl).href : event.request;
    const cache = await caches.open(cacheName);
    cachedResponse = await cache.match(request, { ignoreSearch: shouldServeIndexHtml });
  }
  return cachedResponse || fetch(event.request);
}
