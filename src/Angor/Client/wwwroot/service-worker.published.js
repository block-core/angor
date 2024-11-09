// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}-bsstore`; //${self.assetsManifest.version}
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];
const bc = new BroadcastChannel('angor-channel');

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));

    onAppUpdating();
}

const onAppUpdating = () => {
    bc.onmessage = function (message) {
        if (message && message.data == "skip-waiting") {
            self.skipWaiting();
            bc.postMessage("reload-page");
        }
    }
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    self.skipWaiting(); 

    const cacheKeys = await caches.keys();
    await Promise.all(
        cacheKeys
            .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
            .map(key => caches.delete(key))
    );
}


async function onFetch(event) {
    if (event.request.method === 'GET' && !event.request.url.includes('/api/')) {
        const cache = await caches.open(cacheName);
        const cachedResponse = await cache.match(event.request);

        const networkFetch = fetch(event.request).then(networkResponse => {
            cache.put(event.request, networkResponse.clone());
            return networkResponse;
        });

        return cachedResponse || networkFetch;
    }

    return fetch(event.request);
}

