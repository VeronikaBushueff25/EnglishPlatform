// Service worker Owlish: приложение ставится на телефон и открывается без сети.
// - /api и /health никогда не кешируются: данные всегда свежие;
// - страницы — «сначала сеть», без сети — сохранённая оболочка приложения (index.html);
// - файлы сборки с хешем в имени не меняются, поэтому берутся из кеша, а в кеш попадают при первой загрузке.
// При выпуске новой версии меняется только CACHE: старые кеши удаляются при активации.
const CACHE = 'ep-v3'; // v3 — иконка Owlish из icon.png
const SHELL = '/index.html';
const HASHED = /-[A-Za-z0-9_]{8,}\.(?:js|css|woff2?)$/;

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll([SHELL, '/manifest.webmanifest', '/icons/icon-192.png']))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/health')) return;

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          if (response.ok) {
            const copy = response.clone();
            caches.open(CACHE).then((cache) => cache.put(SHELL, copy));
          }
          return response;
        })
        .catch(() => caches.match(SHELL)),
    );
    return;
  }

  if (HASHED.test(url.pathname)) {
    event.respondWith(
      caches.match(request).then(
        (cached) =>
          cached ??
          fetch(request).then((response) => {
            if (response.ok) {
              const copy = response.clone();
              caches.open(CACHE).then((cache) => cache.put(request, copy));
            }
            return response;
          }),
      ),
    );
  }
});
