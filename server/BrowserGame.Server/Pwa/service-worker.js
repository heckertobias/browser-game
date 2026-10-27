// Vorlage, Platzhalter füllt ServiceWorkerScript.Render; 008 ergänzt push.
const VERSION = "__VERSION__";
const PRECACHE = __PRECACHE__; // ["/play/offline.html", "/play/assets/offline-….js", …]
const OFFLINE_PAGE = "/play/offline.html";
const CACHE = `offline-${VERSION}`;

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(PRECACHE.map((u) => new Request(u, { cache: "reload" })))));
  self.skipWaiting();
});
self.addEventListener("activate", (event) => {
  event.waitUntil(caches.keys()
    .then((keys) => Promise.all(keys.filter((k) => k.startsWith("offline-") && k !== CACHE).map((k) => caches.delete(k))))
    .then(() => self.clients.claim()));
});
const offline = () => caches.match(OFFLINE_PAGE).then((r) => r ?? Response.error());
self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;
  if (request.mode === "navigate") {
    event.respondWith(fetch(request)
      .then((r) => (r.status >= 502 && r.status <= 504 ? offline() : r)) // auch bei 502–504 (Deploy)
      .catch(offline));
    return;
  }
  const url = new URL(request.url);
  if (url.origin === self.location.origin && PRECACHE.includes(url.pathname)) {
    // Cache zuerst: Namen mit Inhalts-Hash; bei 502–504 (Deploy) oder 404 (neuer Server kennt alte Namen nicht)
    // käme die Offline-Seite sonst ohne Stil und Text (007 AK-12, AK-16).
    event.respondWith(caches.match(url.pathname).then((r) => r ?? fetch(request)));
  }
});
