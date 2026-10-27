# 007 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge (keine) → Server → Client → Prüfung. Jeder Task bringt seine Tests mit.

## Doku
- [x] **T1** (AK-1–4): ADR 0003 auf „ersetzt durch ADR 0010“; 001 AK-6 mit Vermerk „Abgelöst durch 007 AK-1–4“;
  Hinweis in ADR 0003 auf die geänderte Vision.

## Server
- [x] **T2** (AK-10, AK-11): `client/scripts/icons.mjs` (PNG per `node:zlib`) und erzeugte Icons in `wwwroot/icons/`,
  `wwwroot/manifest.webmanifest`, Kopf-Tags im `_Layout`. `GameServerFactory` kopiert Manifest und Icons;
  Integrationstests: Manifest-Felder und Content-Type, Icons 200/`image/png`/Größe, Tags auf der Startseite.
- [x] **T3** (AK-12, AK-13): `Pwa/service-worker.js` (Embedded Resource), `ServiceWorkerScript.Render` + Unit-Tests,
  `GET /sw.js` (`text/javascript`, `no-cache`, anonym), `PwaCaching.Apply` für Static Files und `/play`-Fallbacks
  (`no-cache` bzw. `immutable`). Factory schreibt Platzhalter `play/offline.html`; Integrationstests für Header.
- [x] **T4** (AK-9): Spieler-Seiten handytauglich: `_Layout` (`min-h-dvh`, Abstände, `h1` responsiv mit
  Silbentrennung), Umschalter `btn-sm`, Buttons und Linkabstände. Bestehende Seiten-Tests grün; Kurzprüfung in
  DevTools bei 320/360/390 px.

## Client
- [x] **T5** (AK-1–4): `viewport.ts` neu (`chooseScale`, `fitViewport`, `computeViewport`), Konstanten in
  `constants.ts` (ADR 0010), `LOGICAL_*` entfernen. Vitest: Tabelle aus ADR 0010 und Sweep-Invarianten.
- [x] **T6** (AK-1, AK-3–5): `render.ts`/`game.ts` mit variablem Ausschnitt: `cameraOrigin`, Balken und Clip über
  21:9, Culling (`isVisible`), `screenToWorld`; Canvas füllt das Fenster, Größe per ResizeObserver
  (`device-pixel-content-box`, Fallback gerundet) und DPR-Vergleich. Vitest für die reinen Funktionen.
- [x] **T7** (AK-6–8): `styles.css` (touch-action, overscroll-behavior, user-select, touch-callout, tap-highlight,
  `100dvh`), Viewport-Meta, `gesturestart`; HUD mit Safe Areas, `pointer-events-none`, `text-sm`, `btn-lg`.
  `PointerHold` (primärer Zeiger, `lostpointercapture`) + Vitest.
- [x] **T8** (AK-14): `ScreenWakeLock` (Neuanforderung bei Sichtbarkeit und bei Eingabe bis zum Erfolg, Touch per `touchend`, still ohne Unterstützung)
  + Vitest mit Fakes; in `Game.start/stop`.
- [x] **T9** (AK-10–13): `index.html` Kopf-Tags; Offline-Seite (`offline.html`, `src/offline.ts`, Schlüssel
  `offline*` in `i18n.ts`) als zweite Vite-Eingabe; `pwa.ts` registriert `/sw.js` nur im Produktions-Build. Build
  prüfen: absolute Kopf-Links unverändert, `wwwroot/play/offline.html` mit gehashten Assets, `/sw.js` listet sie.

## Prüfung
- [ ] **T10**: Lokal (DevTools: Größen inkl. 3440×1440 und 5120×1440, Offline-Modus, Service Worker im
  Application-Tab) und im RC in der Testumgebung mit Android (Chrome) und iPhone (Safari): AK-1–14 nach der
  Abdeckungstabelle – u. a. installieren mit Name und Icon, Start vom Homescreen an- und abgemeldet, Flugmodus in
  beiden Sprachen, rc.N → rc.N+1 ohne alten Stand, Drehen ohne Verbindungsverlust, Gesten, Wake Lock, Spieler-Seiten
  hochkant. Danach AKs abhaken, Status `umgesetzt`, Roadmap aktualisieren, 003 vermerken (Regeln für HUD, Overlay
  und Account-Seiten).
