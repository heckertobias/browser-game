/** Registriert den Service Worker (007 AK-12, AK-13): nur im Produktions-Build; Fehler ignorieren. */
export function registerServiceWorker(): void {
  if (!import.meta.env.PROD || !("serviceWorker" in navigator)) return;
  navigator.serviceWorker.register("/sw.js", { updateViaCache: "none" }).catch(() => {
    // Ohne Service Worker fehlt nur die Offline-Seite.
  });
}
