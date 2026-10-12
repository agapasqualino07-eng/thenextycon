// Development service worker: no caching, so every reload during development gets fresh files.
self.addEventListener('fetch', () => { });
