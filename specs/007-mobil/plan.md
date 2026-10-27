# 007 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidung: [ADR 0010](../../docs/decisions/0010-sichtfeld-kurze-seite.md)
(Sichtfeld, ersetzt [ADR 0003](../../docs/decisions/0003-logische-aufloesung.md))

## Überblick

```
Client /play ── viewport.ts    Gerätepixel → ganzzahliger Faktor k und sichtbare Welt (kurze Seite ≈ 360, ≤ 400, ≤ 21:9)
             ── render.ts      variabler Ausschnitt, Kamera auf eigener Figur, Culling, dunkle Balken nur über 21:9
             ── game.ts        Canvas füllt das Fenster, Größe per ResizeObserver (device-pixel-content-box), Wake Lock
             ── input.ts       Tippen und Halten: nur primärer Zeiger, pointercancel/lostpointercapture
             ── styles.css     touch-action, overscroll-behavior, user-select, touch-callout, 100dvh, safe-area im HUD
             ── offline.html   „Keine Verbindung“ in beiden Sprachen (Vite-Mehrseiten-Build, Texte aus i18n.ts)
             ── main.tsx       registriert /sw.js (nur Produktions-Build)
             ── version.ts     Version aus VITE_APP_VERSION (Build-Arg APP_VERSION: Release-Tag, sonst Commit-Hash), lokal „dev“
             ── reload.ts      Neu laden mit Rückmeldung „lädt …“; hängt es, Hinweis „Spiel neu öffnen“ (AK-16, auch Offline-Seite)
             ── update.ts      getrennt: /play abfragen, Entry-Script vergleichen → „Neue Version verfügbar“ (AK-17)
Server ────── /manifest.webmanifest, /icons/*   statisch; verlinkt im _Layout und in play/index.html
             ── GET /sw.js     Service Worker aus Vorlage, Version = Hash von play/offline.html
             ── Cache-Header   index.html, offline.html, Manifest: no-cache · /play/assets: immutable
             ── _Layout        Spieler-Seiten handytauglich (dvh, Touch-Ziele, Überschrift bricht um)
Service Worker  Seitenaufrufe gehen ans Netz; schlägt das fehl → Offline-Seite aus dem Cache, ihre Assets zuerst aus dem
                Cache. Sonst wird nichts gecacht.
```

## Entschiedene Fragen

1. **AK-2:** Mit ganzzahligem Faktor und ohne Ränder ist ±1/10 bei niedriger Auflösung nicht einzuhalten (1201–1600
   Gerätepixel auf der kurzen Seite bis −17 %, 801–1200 bis −26 %). Die Spec ist angepasst: nach oben höchstens etwa
   ein Zehntel (hart: 400 Weltpixel), weniger nur bei niedriger Auflösung.
2. **Zielwert 360, Obergrenze 400.** 16:9-Vollbild (720/1080/1440/2160) zeigt exakt 640×360 wie bisher.
3. **`display: standalone`** (Statusleiste sichtbar, keine Browserleiste; iOS kennt `fullscreen` nicht), iOS-Statusleiste
   `black`.
4. **Offline-Seite auch bei 502–504** (Proxy erreicht den Server nicht, z. B. während eines Deploys).
5. **Zoom auf `/play` gesperrt** (`user-scalable=no`, `touch-action: none`); die Spieler-Seiten bleiben zoombar.
6. **Platzhalter-Icon und Name `Browser Game`**, ersetzen, sobald es Grafik bzw. einen Namen gibt.
7. **Nach einem Abbruch ein Tipp, keine Automatik (AK-16):** Rückmeldung „lädt …“ und nach einer Wartezeit der Hinweis,
   das Spiel neu zu öffnen. Kein automatisches Neuladen (keine Schleifen) und kein zweiter Navigationsversuch, bis eine
   Diagnose am iPhone zeigt, dass er nötig ist (RC rc.4: Tipp auf „Neu verbinden“ ohne jede Wirkung, Ursache offen).

## Verträge

**Protobuf:** keine Änderung. Der Server kennt die Bildschirmgröße nicht und braucht sie nicht (ADR 0010).

**OpenAPI:** keine Änderung. Manifest und Service Worker sind statische bzw. generierte Dateien, keine JSON-API.

**AK-16, AK-17:** keine Änderung. Die Update-Erkennung lädt das HTML von `/play` (keine JSON-API) und vergleicht das
Entry-Script (Dateiname mit Inhalts-Hash) mit dem laufenden. Verworfen: Version im `Welcome` (Protobuf) – der Server
müsste die Version zur Laufzeit kennen, und sie käme erst nach einem Wiederverbinden an; `version.json` – wäre eine
HTTP-API (OpenAPI) und bräuchte Build-Code in `vite.config.ts`. Ein späterer Wartungsmodus kündigt sich voraussichtlich
per `ServerMessage` an; dann ändert sich das Protobuf.

## Sichtfeld (AK-1–5, ADR 0010)

### Regel
- Gerechnet wird in **Gerätepixeln** (CSS-Pixel × `devicePixelRatio`). Ein Weltpixel ist `k` Gerätepixel, `k`
  ganzzahlig ≥ 1 → alle Pixel gleich groß und scharf (AK-3).
- `k` macht die kurze Seite möglichst nah an **360** Weltpixel (relativ gemessen), aber nie über **400**.
- Das Canvas ist immer so groß wie das Fenster (AK-1). Die lange Seite zeigt `Gerätepixel / k` – höchstens 21:9 der
  kurzen Seite; darüber hinaus bleibt das Canvas dunkel (AK-4).

### Algorithmus (`client/src/game/viewport.ts`, ersetzt `computeViewport`)

```ts
// constants.ts – an genau einer Stelle (ADR 0010); LOGICAL_WIDTH/HEIGHT entfallen
export const TARGET_SHORT_SIDE = 360;
export const MAX_SHORT_SIDE = 400;
export const MAX_ASPECT_LONG = 21;
export const MAX_ASPECT_SHORT = 9;

export interface Viewport {
  scale: number;        // Gerätepixel je Weltpixel, ganzzahlig ≥ 1
  canvasWidth: number;  // Gerätepixel = ganzes Fenster
  canvasHeight: number;
  viewWidth: number;    // sichtbare Welt in Weltpixeln (darf gebrochen sein)
  viewHeight: number;
  offsetX: number;      // Gerätepixel bis zum Spielbild; > 0 nur über 21:9 (AK-4)
  offsetY: number;
}

export function chooseScale(shortSide: number): number {
  const ratio = shortSide / TARGET_SHORT_SIDE;
  let scale = Math.max(1, Math.floor(ratio));
  if (ratio > Math.sqrt(scale * (scale + 1))) scale++;  // relativ näher an 360 (Grenze: geometrisches Mittel)
  if (shortSide / scale > MAX_SHORT_SIDE) scale++;       // Fairness: nie mehr als 400
  return scale;
}

export function fitViewport(deviceWidth: number, deviceHeight: number): Viewport {
  const width = Math.max(1, Math.round(deviceWidth));
  const height = Math.max(1, Math.round(deviceHeight));
  const short = Math.min(width, height);
  const scale = chooseScale(short);
  const maxLong = Math.floor((short * MAX_ASPECT_LONG) / MAX_ASPECT_SHORT);
  const drawWidth = width >= height ? Math.min(width, maxLong) : width;
  const drawHeight = width >= height ? height : Math.min(height, maxLong);
  return {
    scale, canvasWidth: width, canvasHeight: height,
    viewWidth: drawWidth / scale, viewHeight: drawHeight / scale,
    offsetX: Math.floor((width - drawWidth) / 2), offsetY: Math.floor((height - drawHeight) / 2),
  };
}

/** Bequemlichkeit für Tests und den Fallback ohne device-pixel-content-box. */
export function computeViewport(cssWidth: number, cssHeight: number, devicePixelRatio: number): Viewport {
  return fitViewport(cssWidth * devicePixelRatio, cssHeight * devicePixelRatio);
}
```

Nach dem Hochschalten wegen „relativ näher“ kann die Obergrenze nicht mehr greifen (`s/(k+1) < 360·√(k/(k+1)) < 400`);
ein Schritt genügt. Beispiele und Abweichungen: Tabelle in ADR 0010.

### Nicht ganzzahliger DPR, Browser-Zoom, kleine Fenster
- **DPR 2,625 u. ä.:** `k` zählt Gerätepixel, also bleibt jedes Weltpixel genau `k` Gerätepixel – sofern der
  Canvas-Puffer 1:1 auf Gerätepixel fällt. Dafür misst `game.ts` die Größe mit
  `ResizeObserver(…).observe(canvas, { box: "device-pixel-content-box" })` (`devicePixelContentBoxSize`, Chrome/Edge/
  Firefox). Ohne Unterstützung (Safari) `Math.round(clientWidth × dpr)`; iOS hat ganzzahlige DPR, dort ist das exakt.
- **Browser-Zoom** ändert CSS-Pixel und DPR, nicht die Gerätepixel des Fensters → gleicher Ausschnitt. Herauszoomen
  zeigt also nicht mehr Welt; das HUD (CSS) wächst mit dem Zoom.
- **Sehr kleine Fenster:** nie fraktional verkleinern (anders als ADR 0003). Kurze Seite ≤ 400 Gerätepixel → `k = 1`,
  man sieht weniger (z. B. 480×270 → 480×270 Welt). 401–800 → `k = 2` (200–400). Mehr als 400 sieht man nie.
- Fenstergröße 0 (minimiert): Größe nicht übernehmen, nicht zeichnen.

### Rendering (`render.ts`, `game.ts`)
- `cameraOrigin(camera, viewport)`: `round((camera − view/2) · k) / k` je Achse, mit `viewWidth/viewHeight` statt
  640×360.
- `render`: Transform zurücksetzen; nur wenn `offsetX|offsetY > 0`: Canvas schwarz füllen und auf das Spielbild
  (`offset`, `view × k`) clippen. Dann `setTransform(k, 0, 0, k, offsetX − origin.x·k, offsetY − origin.y·k)`,
  Hintergrund und Gitter über `viewWidth × viewHeight`.
- **Culling:** `isVisible(player, origin, viewport, margin)` – Figur samt Namen (Rand `CULL_MARGIN = 64` Weltpixel)
  muss das Sichtfeld schneiden. Bei ≤ 50 Spielern billig, aber der Ausschnitt ist jetzt bis 933×400 groß.
- **Bildschirm → Welt:** reine Funktion `screenToWorld(x, y, cssWidth, cssHeight, viewport, origin)`:
  `origin + (x · canvasWidth / cssWidth − offsetX) / k` (y analog). Ersetzt `Game.toWorld` mit `LOGICAL_*`.
- **Canvas:** `display:block; width:100%; height:100%` im Vollbild-Container; JS setzt nur noch `canvas.width/height`
  (keine `style.width`). `image-rendering: pixelated` bleibt als Netz.
- **AK-5:** Der ResizeObserver meldet jede Änderung (Drehen, Fenstergröße, Browserleisten); `draw` übernimmt sie im
  nächsten Frame. Ändert sich nur der DPR (Fenster auf anderen Monitor), erkennt der Frame-Vergleich von
  `devicePixelRatio` das. Die WebSocket-Verbindung bleibt unberührt.

### Server
Keine Änderung. Heute bekommt jeder alle Spieler; kommt später Interest-Management, muss es den größten Ausschnitt
nach ADR 0010 abdecken (siehe dort) – ohne die Bildschirmgröße vom Client zu übernehmen (Constitution §2).

## Server

### Manifest und Icons (AK-10, AK-11)
- `wwwroot/manifest.webmanifest` (statisch, Content-Type `application/manifest+json` aus dem Standard-Mapping; im Test
  prüfen):
  ```json
  {
    "id": "/play",
    "name": "Browser Game",
    "short_name": "Browser Game",
    "start_url": "/play",
    "scope": "/",
    "display": "standalone",
    "background_color": "#000000",
    "theme_color": "#000000",
    "icons": [
      { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
      { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
      { "src": "/icons/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
    ]
  }
  ```
  `scope: "/"`: Anmeldung (`/Account/Login`) und Spiel liegen im selben App-Fenster; mit `/play/` würde die
  Weiterleitung zur Anmeldung eine Browserleiste zeigen. Das Admin läuft auf eigenem Host (ADR 0006), ist also nie im
  Scope. Keine `orientation` (beide erlaubt), keine `description` (wäre sprachabhängig).
- **Icons** `wwwroot/icons/`: `icon-192.png`, `icon-512.png`, `icon-maskable-512.png` (Motiv im inneren 60 %,
  Hintergrund vollflächig), `apple-touch-icon.png` (180×180, ohne Transparenz). Erzeugt von
  `client/scripts/icons.mjs` (Pixelraster 16×16 als Text, ganzzahlig skaliert, PNG per `node:zlib` – `deflateSync`,
  `crc32`; keine Abhängigkeit), Ergebnis eingecheckt. npm-Skript `icons`, läuft nur bei Änderungen, nicht im Build.
  ESLint: Datei mit `disableTypeChecked` und Node-Globals oder in `ignores`.
- **Kopf-Tags** im `_Layout` (und gleich in `client/index.html`, siehe Client):
  ```html
  <link rel="manifest" href="/manifest.webmanifest" />
  <meta name="theme-color" content="#000000" />
  <link rel="icon" type="image/png" href="/icons/icon-192.png" />
  <link rel="apple-touch-icon" href="/icons/apple-touch-icon.png" />
  <meta name="apple-mobile-web-app-title" content="Browser Game" />
  <meta name="mobile-web-app-capable" content="yes" />
  <meta name="apple-mobile-web-app-capable" content="yes" />
  <meta name="apple-mobile-web-app-status-bar-style" content="black" />
  ```
  Im Layout `content` von Titel aus `T["Common.GameName"]` (ADR 0008: kein Text im Markup).

### Service Worker (AK-12, AK-13)
- **Handgeschrieben, keine Abhängigkeit** (kein `vite-plugin-pwa`/Workbox – Constitution §5): Wir brauchen nur
  „Seitenaufruf → Netz, sonst Offline-Seite“. Ein generierter Precache des ganzen Clients wäre mehr Code und genau die
  Quelle hängender Stände, die AK-13 verbietet.
- **Vorlage** `server/BrowserGame.Server/Pwa/service-worker.js` als `EmbeddedResource` (wie `blocked-names.txt`):
  ```js
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
      event.respondWith(caches.match(url.pathname).then((r) => r ?? fetch(request))); // Cache zuerst (Hash im Namen)
    }
  });
  ```
  Alle anderen Anfragen (Assets des Spiels, WebSocket, Formulare) laufen am Service Worker vorbei.
- **`ServiceWorkerScript.Render(string? offlineHtml)`** (reine Funktion, `Pwa/ServiceWorkerScript.cs`):
  - Assets der Offline-Seite per `[GeneratedRegex]` `/play/assets/[A-Za-z0-9._-]+` aus dem HTML, dazu
    `/play/offline.html`; als JSON (`System.Text.Json`) in `__PRECACHE__`.
  - `VERSION` = erste 16 Hex-Zeichen von SHA-256 über das HTML. Weil die Asset-Namen Inhalts-Hashes tragen, ändert
    jede Änderung an Offline-Seite, Texten oder CSS das HTML → neue `/sw.js` → Browser installiert neu, `activate`
    löscht den alten Cache.
  - Fehlt `play/offline.html` (z. B. ohne Client-Build): `PRECACHE = []`, `VERSION = "none"` – kein Offline-Fallback,
    aber nichts kaputt.
- **Endpunkt** `app.MapGet("/sw.js", …)` vor den Fallbacks: liest `play/offline.html` über
  `IWebHostEnvironment.WebRootFileProvider` (pro Anfrage; Datei ~1 KB), `Content-Type: text/javascript; charset=utf-8`,
  `Cache-Control: no-cache`, anonym. Keine statische `wwwroot/sw.js` (würde den Endpunkt verdecken).
  `Service-Worker-Allowed` ist nicht nötig: Skript liegt im Root, Scope `/`.
- **skipWaiting + clients.claim** sind gefahrlos: Der Service Worker liefert App-Code höchstens als Datei mit
  Inhalts-Hash aus (mit dem Spiel geteilte Chunks der Offline-Seite, z. B. `styles-*.css`) – gleicher Name, gleicher
  Inhalt –, es gibt also keine gemischten Stände.
- **Assets der Offline-Seite zuerst aus dem Cache (AK-12, AK-16):** Bei einem Deploy antwortet der Proxy mit 502–504,
  danach kennt der neue Server die alten Asset-Namen nicht (404). Mit „Netz zuerst, Cache nur bei Netzfehler“ erschiene
  die Offline-Seite dann ohne Stil und ohne Text. Ihre Assets tragen Inhalts-Hashes, Cache zuerst ist also immer
  richtig; fehlt ein Eintrag, geht die Anfrage ans Netz.
  - Nichts bleibt veraltet liegen: Der Cache heißt nach dem Hash von `offline.html` und enthält genau sie und ihre
    Assets. Ändert sich `offline.html` nicht, bleiben Namen und Inhalte gültig. Ändert sie sich, gibt es eine neue
    `/sw.js`, ein neuer Cache wird angelegt, `activate` löscht den alten. Bis dahin passen alte Offline-Seite und alte
    Assets zueinander – genau das braucht der Deploy-Fall.
  - Eine Änderung der Vorlage ändert `/sw.js` Byte für Byte; installierte Clients übernehmen den neuen Service Worker
    beim nächsten Start (gleicher Cache-Name, wird neu gefüllt).
- 008 (Push) ergänzt später `push`/`notificationclick` in derselben Vorlage.

### Cache-Header (AK-13)
- Eine Funktion `PwaCaching.Apply(StaticFileResponseContext)` für `UseStaticFiles` und beide `MapFallbackToFile`
  (Überladung mit `StaticFileOptions`; der Fallback schreibt den Pfad auf `/play/index.html` um, dieselbe Prüfung
  greift):
  - `/play/assets/*` → `public, max-age=31536000, immutable` (Dateinamen mit Hash).
  - `/play/index.html`, `/play/offline.html`, `/manifest.webmanifest` → `no-cache`.
  - Sonst unverändert. Razor-Seiten sind dynamisch (Formularseiten setzen über Antiforgery ohnehin `no-store`).
- Ergebnis: Ein Start lädt immer das aktuelle `index.html` und damit das aktuelle Bundle; der Service Worker cacht
  kein App-HTML und vom Bundle nur die mit der Offline-Seite geteilten Dateien (Inhalts-Hash). Läuft eine alte Sitzung
  weiter (App im Hintergrund), trennt der Deploy den WebSocket; „Neu verbinden“ lädt neu und damit die neue Version,
  vorher meldet das Overlay „Neue Version verfügbar“ (AK-16, AK-17, siehe „Verbindungsabbruch und Update“).

### CSP
Keine Änderung. `manifest-src` und `worker-src` fallen auf `default-src 'self'` zurück; Offline-Seite und Client
nutzen nur externe Skripte und Stylesheets. Gecachte Antworten behalten ihre CSP-Header.

### Anmeldung beim Start (AK-11)
Unverändert: `/play` verlangt Anmeldung, ohne Session 302 auf `/Account/Login?ReturnUrl=%2Fplay`, danach zurück ins
Spiel. Der Service Worker reicht Seitenaufrufe nur durch (Weiterleitungen bei Navigationen sind erlaubt). Hinweis:
iOS gibt Homescreen-Apps einen eigenen Cookie-Speicher – dort meldet man sich ggf. einmal in der App an.

### Spieler-Seiten am Handy (AK-9, `_Layout.cshtml` und Seiten)
- `body`: `min-h-dvh` statt `min-h-screen` (iOS-100vh), `p-2 sm:p-4`.
- `h1`: `text-2xl sm:text-3xl break-words hyphens-auto` – „BESTÄTIGUNGSMAIL“ in `text-3xl` mono mit
  `tracking-widest` ist breiter als ein 390-px-Handy.
- Sprachumschalter `btn-xs` → `btn-sm`; `Index`: `btn-sm` → `btn`; Linklisten `gap-1` → `gap-3`.
- Eingabefelder: daisyUI 5 setzt bei Fokus auf Touch-Geräten 16 px (kein iOS-Zoom); `w-full` und `autocomplete` sind
  schon da.
- Viewport-Meta bleibt `width=device-width, initial-scale=1` (zoombar, ohne `viewport-fit=cover` – Safari hält Inhalt
  aus Aussparungen heraus).

## Client

### Touch und Gesten (AK-8)
- `styles.css` (nur Spiel und Offline-Seite):
  `html, body { overflow: hidden; overscroll-behavior: none; touch-action: none; -webkit-user-select: none;
  user-select: none; -webkit-touch-callout: none; -webkit-tap-highlight-color: transparent; }`, `#root { height: 100dvh; }`.
  `touch-action` wird entlang der Vorfahren geschnitten – Buttons bleiben klickbar, aber nichts im Spiel scrollt oder
  zoomt; Doppeltipp-Zoom und Pull-to-Refresh entfallen.
- iOS ignoriert `user-scalable=no` beim Pinch: zusätzlich `gesturestart`/`gesturechange` → `preventDefault()` (WebKit).
- `index.html` Viewport: `width=device-width, initial-scale=1, viewport-fit=cover, user-scalable=no` .
- Kontextmenü auf dem Canvas ist schon unterdrückt (`contextmenu`).

### HUD (AK-6)
- Vollbild-Container `fixed inset-0 bg-black`, Canvas darin; HUD als Ebene mit `pointer-events-none` (Touches gehen
  aufs Canvas), interaktive Elemente `pointer-events-auto`.
- Abstände aus den Safe Areas: `top-[max(0.5rem,env(safe-area-inset-top))]`,
  `left-[max(0.5rem,env(safe-area-inset-left))]` usw.
- Schrift im HUD mindestens `text-sm` (14 px); Buttons (`Neu verbinden`, später `kicked`-Overlay) `btn-lg`
  (48 px Höhe).
- Bei Formaten über 21:9 sitzt das HUD am Fensterrand (im dunklen Bereich), nicht am Spielbild.

### Eingabe (AK-7, `input.ts`)
Heute schon richtig: ein Zeiger zur Zeit, `setPointerCapture`, Folgen bei `pointermove`, Stopp bei `pointerup`/
`pointercancel`, Reset bei `blur`/`visibilitychange`. Ergänzungen:
- Kleine reine Klasse `PointerHold` (`down`, `move`, `release`, `reset`, `position`) statt der Felder in
  `InputTracker` – ohne DOM testbar.
- Nur `isPrimary && button === 0` startet (keine rechte/mittlere Maustaste; bei Touch der erste Finger).
- `lostpointercapture` gibt frei wie `pointerup` (Capture kann ohne `pointerup` verloren gehen).
- Mehrfinger: weitere Finger werden ignoriert; hebt man den ersten, bleibt die Figur stehen (AK-7 „lasse ich los“).

### Offline-Seite (AK-12)
- `client/offline.html` + `src/offline.ts` als zweite Eingabe des Vite-Builds (Mehrseiten:
  `build.rolldownOptions.input` bzw. `rollupOptions` – `index.html`, `offline.html`) → `wwwroot/play/offline.html` mit
  gehashten Assets. Stil: `styles.css` (Tailwind/daisyUI wie das Spiel).
- Texte im Client-Wörterbuch (ADR 0008): `offlineTitle` („Keine Verbindung“/„No connection“), `offlineMessage`
  („Prüfe deine Internetverbindung.“/„Check your internet connection.“), `offlineRetry` („Erneut versuchen“/„Try again“).
  `tsc` erzwingt beide Sprachen.
- `offline.ts`: setzt `document.documentElement.lang = language` und `textContent` aus `t`; Sprache wie im Spiel über
  `detectLanguage(document.cookie, navigator.language)` – das Culture-Cookie ist lesbar und offline vorhanden, der
  Service Worker muss die Sprache nicht kennen. „Erneut versuchen“ → `location.reload()` (die Seite steht unter der
  ursprünglichen URL, z. B. `/play`).

### Web-App-Kopf und Registrierung (AK-10, AK-11)
- `client/index.html`: dieselben Kopf-Tags wie im `_Layout` (fester Text `Browser Game`). Nach dem Build prüfen, dass
  Vite die absoluten Pfade (`/manifest.webmanifest`, `/icons/…`) unverändert lässt (`base: "/play/"`); sonst
  `vite-ignore`.
- `src/pwa.ts`: `registerServiceWorker()` – nur `import.meta.env.PROD` und wenn `"serviceWorker" in navigator`;
  `navigator.serviceWorker.register("/sw.js", { updateViaCache: "none" })`, Fehler ignorieren. Aufruf in `main.tsx`.
  Registriert wird beim ersten Spielstart (`start_url`); danach deckt der Scope `/` auch die Spieler-Seiten ab.
- Lokal testen: `npm run build`, `dotnet run`, `http://localhost:5080/play` (localhost gilt als sicherer Kontext).

### Bildschirm an (AK-14, `game/wakeLock.ts`)
- `ScreenWakeLock` mit injizierbarer API (`navigator.wakeLock`) und `document`:
  - `start()`: anfordern, wenn `visibilityState === "visible"`; bei `visibilitychange` → sichtbar erneut anfordern
    (der Browser gibt die Sperre beim Verstecken selbst frei); zusätzlich bei jedem `keydown`, `pointerup` (Maus,
    Stift) bzw. `touchend` (Touch), solange keine Sperre besteht. WebKit verlangt für die erste Sperre pro Seite eine
    Nutzergeste, danach geht die Neuanforderung bei Sichtbarkeit ohne. Touch-`pointerup` fordert nicht an, damit es
    das `touchend` desselben Tippens nicht blockiert.
  - `stop()`: Listener weg, Sperre freigeben.
  - Nicht unterstützt oder abgelehnt (`NotAllowedError`, Akku-Sparmodus): still nichts tun – dann gilt die normale
    Einstellung. Kein NoSleep-Video-Trick (Abhängigkeit/Asset, Akku).
- In `Game.start()`/`stop()`.

### Version (AK-15)
- **Im Bundle, nicht vom Server:** Angezeigt wird, was das Gerät ausführt. Eine Server-Abfrage zeigte die Version
  des Servers und könnte einen hängenden alten Stand (AK-13) nicht aufdecken.
- **Fluss:** CI-Job `docker`, Schritt `version` → `build-args: APP_VERSION=…` bei **allen drei** Builds (Scan amd64,
  Scan arm64, Push) → Dockerfile Stage `client`: `ARG APP_VERSION` direkt vor `RUN … npm run build`, dort als
  `VITE_APP_VERSION` → Vite ersetzt `import.meta.env.VITE_APP_VERSION` statisch → `version.ts`.
  - Tag-Build (`PUBLISH`): `steps.meta.outputs.version` (gleicher Wert wie das Image-Tag, z. B. `0.4.0-rc.3`),
    Format geprüft wie in `release.sh`.
  - Sonst (PR, main, nächtlich, manuell): die ersten 7 Zeichen des Commit-Hashs, damit jeder Build eindeutig
    zuordenbar ist. Bei PRs `github.event.pull_request.head.sha` statt `github.sha`: Dieser ist der flüchtige
    Merge-Commit, der in keiner Branch-Historie steht. Gebaut wird zwar der Merge-Stand, aber PR-Images werden nie
    veröffentlicht (ADR 0002); das Label identifiziert den Commit.
- **Lokal `dev`** (genau eine Stelle: `formatVersion`, wenn der Wert leer ist): `npm run dev`/`npm run build` und
  `docker build` ohne Argument. `.git` fehlt im Docker-Kontext, eine lokale Arbeitskopie ist oft nicht committet,
  und solche Builds werden nie ausgerollt. Kein `child_process`/`@types/node` in `vite.config.ts`.
- **Gleicher Build-Arg in Scan- und Push-Builds:** Nur so übernimmt der Push-Build die Schichten aus dem gha-Cache,
  und das veröffentlichte Image ist das gescannte.
- **Docker-Cache:** `ARG` gilt nur in der Stage `client` und steht nach `npm ci`. Weil sich der Wert mit jedem Commit
  ändert, laufen bei jedem CI-Build der Client-Build und – über den geänderten `wwwroot` – `dotnet publish` des
  Spiels (je Architektur) neu. `npm ci`, Restore und das Admin-Publish bleiben im Cache. Aufschlag: Sekunden bis etwa
  eine Minute je Matrix-Job, hingenommen für die Eindeutigkeit. Den Wert erst nach dem Build einzusetzen (`sed`),
  hielte den Cache, ließe aber den Asset-Namen gleich (`immutable`) – das widerspricht AK-13.
- **Content-Hash:** Der String steckt im Spiel-Chunk (nur `App.tsx` importiert `version.ts`) → jede Version (Release oder
  Commit) hat einen neuen Asset-Namen, gewollt für AK-13. `offline.html` und ihre Assets bleiben gleich → die SW-Version ändert sich
  nicht bei jedem Release; richtig so, der SW cacht keinen App-Code.
- **Anzeige:** `App.tsx`, letztes Kind der HUD-Ebene (also auch über dem Overlay „getrennt“), unten rechts mit
  Safe-Area-Abstand, `text-xs normal-case text-base-content/60`, keine Badge, nicht interaktiv. Ohne Präfix
  (`0.4.0-rc.3`). `normal-case` ist Pflicht, die HUD-Ebene setzt `uppercase`.
- **12 px statt 14 px:** AK-6 meint Anzeigen und Buttons, die man zum Spielen braucht. Die Version ist passiv und soll
  laut AK-15 „klein und unaufdringlich“ sein.
- **Kein i18n:** Die Versionsnummer ist ein Wert, kein Text; `dev` ist ein technischer Bezeichner.
- **Typ:** `src/vite-env.d.ts` deklariert `ImportMetaEnv.VITE_APP_VERSION?: string`. Kein `define` in
  `vite.config.ts` (kein `@types/node`, keine neue Abhängigkeit).

### Verbindungsabbruch und Update (AK-16, AK-17)
Anlass: Im RC (rc.3 → rc.4, iPhone, Homescreen-App) bewirkte ein Tipp auf „Neu verbinden“ lange nach dem Deploy
nichts – kein Flackern, kein Neuladen. Ob der Tipp nicht ankommt, `location.reload()` ignoriert wird oder die
Navigation hängt, ist offen; ohne Rückmeldung sehen alle drei gleich aus. Wiederverbinden gab es bisher nur über diesen
Button (`net.ts` meldet `onClose`, sonst nichts).

- **`src/reload.ts`** – `reloadPage(win, onStuck)`: startet einen Timer (`RELOAD_STUCK_MS = 8000`) und ruft
  `location.reload()`. Wechselt die Seite, stirbt der Timer mit ihr; lebt sie nach 8 s noch, ruft er `onStuck`.
  Der Aufrufer zeigt vorher „lädt …“ (Button deaktiviert) und bei `onStuck` den Hinweis „Passiert nichts? Schließe das
  Spiel und öffne es neu.“ (Button wieder aktiv). Kein zweiter Navigationsversuch (Entschiedene Frage 7).
  Genutzt im Overlay „getrennt“ und für „Erneut versuchen“ auf der Offline-Seite (`offline.ts`, Hinweis in
  `#offline-hint`); später auch im `kicked`-Overlay (003).
- **`src/update.ts`** (ohne DOM testbar):
  - `entryScript(html)`: `src` des ersten `<script type="module" … src>` (Reihenfolge der Attribute egal), sonst `null`.
    Vite schreibt genau ein solches Skript (`/play/assets/index-<hash>.js`); der Hash ändert sich mit jeder Version
    (AK-15).
  - `classifyServer(current, response, html)`: nicht `ok` (Netz, 502–504, 404 …) → `unreachable`; umgeleitet (z. B.
    Anmeldung abgelaufen) → `same` (der Tipp führt zur Anmeldung, kein falscher Update-Hinweis); Entry-Script bekannt
    und anders als `current` → `update`; sonst `same`.
  - `ServerWatch` (Abhängigkeiten injizierbar wie bei `ScreenWakeLock`: `fetch`, Timer, `document`, `window`): läuft,
    solange der Status `disconnected` ist. Fragt `GET /play` (`cache: "no-store"`, Timeout 5 s per
    `AbortController`) sofort, dann alle `SERVER_POLL_MS = 5000`, außerdem sofort bei `online` und bei
    `visibilitychange` → sichtbar. Nur sichtbar, nie zwei Anfragen gleichzeitig. Bei `update`: einmal `onUpdate`,
    danach Schluss.
  - Das laufende Entry-Script liest `App.tsx` aus dem DOM (`script[type="module"][src]`). Im Dev-Modus sind beide
    gleich → nie ein Update-Hinweis.
  - Die Anfrage aus der Seite ist keine Navigation und steht nicht im Precache, der Service Worker reicht sie also
    nur durch.
- **Overlay `App.tsx`** (Status `disconnected`):
  - normal: Button „Neu verbinden“;
  - `update`: Satz „Neue Version verfügbar. Bitte neu laden.“ über dem Button „Neu laden“;
  - nach dem Tipp: Button „lädt …“, deaktiviert; nach 8 s ohne Seitenwechsel: Hinweis darunter, Button wieder aktiv.
  - Sätze `normal-case`, mindestens `text-sm` (AK-6); Button wie bisher `btn-lg`. Die Version bleibt letztes Kind der
    HUD-Ebene (AK-15).
- **Kein automatisches Neuladen** (Spec, Entschiedene Fragen): Der Spieler tippt; die Abfrage ändert nur den Text.
- **Texte** (`i18n.ts`, ADR 0008): `reloading`, `reloadStuck`, `updateAvailable`, `updateReload`.

## Regeln für 003 und spätere UI
HUD-Elemente und Overlays liegen im Safe-Area-Container, Touch-Ziele ≥ 44 px (`btn-lg` bzw. `min-h-11`), Schrift ≥ 14 px,
nichts nur per Hover bedienbar; Account-Seiten nutzen das `_Layout` und prüfen 320/360/390 px Breite.
Ausnahme: rein informative Anzeigen ohne Spielrelevanz (z. B. Version, AK-15) dürfen `text-xs` (12 px) sein,
liegen aber ebenfalls im Safe-Area-Container und sind nie interaktiv.
Neu laden aus Overlays (z. B. „Hier weiterspielen“) immer über `reloadPage` (Rückmeldung, Hinweis bei Hängen, AK-16).

## Tests

- **Vitest**
  - `viewport.test.ts` (ersetzt die 001-Tests): Tabelle aus ADR 0010 (u. a. 1280×720, 1920×1080, 2560×1440, MacBook,
    iPhone hochkant/quer, Android gerundet und exakt 1080×2400, Ultrawide, 32:9, kleine Fenster). Invarianten über alle
    Größen 200…4000 × 200…4000 (Schritt z. B. 37) und mehrere DPR: `scale` ganzzahlig ≥ 1; Canvas = Gerätegröße;
    `view × scale + 2·offset` = Canvas (±1); kurze Sicht ≤ 400; lange/kurze ≤ 21/9 (+1 Gerätepixel); bei kurzer Seite
    ≥ 1601 Gerätepixeln kurze Sicht ≥ 320 (−11 %, ADR 0010); Offsets nur über 21:9.
  - `render.test.ts`: `cameraOrigin` (gerundet auf Gerätepixel, Kamera mittig), `screenToWorld` (Mitte → Kamera, mit
    Offset), `isVisible` (Rand, Name).
  - `input.test.ts`: `PointerHold` – zweiter Finger ignoriert, nicht primärer/rechte Taste ignoriert, `release` mit
    fremder ID wirkungslos, `lostpointercapture`/`cancel` stoppen.
  - `wakeLock.test.ts` mit Fakes: fordert bei Start an, erneut bei Sichtbarkeit, nicht wenn verborgen, gibt bei
    `stop` frei, wirft nicht ohne API oder bei Ablehnung; nach Ablehnung erneut bei jeder Eingabe bis zum Erfolg;
    `touchend` statt Touch-`pointerup`; ein Versuch pro Tippen; erneut nach Freigabe ohne `visibilitychange`.
  - `version.test.ts`: `formatVersion` – `undefined`, `""`, `"  "` → `dev`; `"0.4.0-rc.3"`, `"0.4.0"` und ein
    Commit-Hash (`"5539bc0"`) unverändert.
  - `i18n.test.ts`: Offline-Schlüssel in beiden Sprachen nicht leer (Rest erzwingt `tsc`).
  - `reload.test.ts` mit Fake-Fenster: lädt sofort einmal neu; `onStuck` erst nach `RELOAD_STUCK_MS`, genau einmal.
  - `update.test.ts`: `entryScript` (echte Vite-Ausgabe mit `crossorigin`, `src` vor `type`, einfache
    Anführungszeichen, Seite ohne Modul-Skript → `null`, Modul-Skript ohne `src` übersprungen); `classifyServer`
    (502/503/504/404 → `unreachable`, umgeleitet → `same`, gleich → `same`, anders → `update`, unbekannt → `same`);
    `ServerWatch` mit Fakes: fragt sofort, wiederholt nach 5 s, Timeout nach 5 s zählt als nicht erreichbar, nicht
    wenn verborgen, sofort bei Sichtbarkeit und `online`, nie parallel, `update` → einmal `onUpdate` und Schluss,
    `stop` beendet alles (auch eine noch laufende Antwort).
  - `i18n.test.ts`: neue Schlüssel (`reloading`, `reloadStuck`, `updateAvailable`, `updateReload`) in beiden Sprachen
    nicht leer.
- **Server (xUnit)**
  - `ServiceWorkerScriptTests` (Unit): Assets aus HTML, Version ändert sich mit dem HTML, gleiche Eingabe → gleiche
    Ausgabe, ohne HTML leerer Precache, Ausgabe enthält keine Platzhalter mehr, vorgehaltene Assets zuerst aus dem
    Cache (Regressionsanker, AK-12/16).
  - `PwaTests` (Integration): `/manifest.webmanifest` → 200, `application/manifest+json`, Felder (`start_url`,
    `scope`, `display`, `id`), jedes Icon 200 `image/png` mit Größe laut IHDR = `sizes`; `apple-touch-icon` 180×180;
    `/sw.js` anonym 200, `text/javascript`, `no-cache`, enthält Asset aus Platzhalter-`offline.html`;
    `/play/offline.html` anonym 200, `no-cache`; `/play` angemeldet `no-cache`; `/play/assets/test-asset.js`
    `immutable`; Startseite enthält Manifest-, Icon- und Theme-Tags.
  - `GameServerFactory`: kopiert `manifest.webmanifest` und `icons/` aus dem echten `wwwroot` des Server-Projekts
    (Suche nach `BrowserGame.slnx` ab `AppContext.BaseDirectory`), schreibt Platzhalter
    `play/offline.html` mit `<script src="/play/assets/test-asset.js">`.
  - Bestehend: `Play_redirects_anonymous_to_login` (AK-11), `Responses_carry_security_headers`.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Canvas = Fenster in Gerätepixeln, Ausschnitt aus `fitViewport` | Vitest-Invarianten; **manuell:** Monitor, Laptop, Handy hochkant/quer, keine Ränder |
| 2 | `chooseScale`: nah an 360, nie über 400 | Vitest-Tabelle und Sweep; **manuell:** gleicher Ausschnitt (Gitter: 32er-Raster zählen) auf ≥ 3 Geräten |
| 3 | ganzzahliges `k`, Puffer per `device-pixel-content-box` | Vitest (`scale` ganzzahlig, Puffer = Gerätegröße); **manuell:** Android (DPR 2,625) und Browser-Zoom, Screenshot vergrößert |
| 4 | lange Seite ≤ 21:9, Rest schwarz, Spielbild mittig | Vitest (32:9, Ultrawide, Offsets); **manuell:** DevTools 3440×1440 und 5120×1440 |
| 5 | ResizeObserver + DPR-Vergleich je Frame, WebSocket unberührt | **manuell:** drehen, Fenster ziehen – Status bleibt „verbunden“, Figur bleibt |
| 6 | HUD mit safe-area, `text-sm`, `btn-lg` | **manuell:** iPhone mit Aussparung quer/hochkant, Android mit Gestenleiste |
| 7 | `PointerHold`, primärer Zeiger, cancel/lostpointercapture | Vitest; bestehende `pointerDirection`-Tests; **manuell:** halten, ziehen, loslassen, zweiter Finger |
| 8 | `touch-action`, `overscroll-behavior`, `user-select`, `touch-callout`, `gesturestart`, Viewport | **manuell:** Pinch, Doppeltipp, Pull-to-Refresh, langes Drücken, Textauswahl (Android und iPhone) |
| 9 | `_Layout` mit dvh, responsiver Überschrift, größeren Touch-Zielen | bestehende Seiten-Tests grün; **manuell:** 320/360/390 px, beide Sprachen, alle Spieler-Seiten |
| 10 | Manifest, Icons, Kopf-Tags | Integration `PwaTests`; **manuell:** Android „App installieren“, iPhone „Zum Home-Bildschirm“ mit Name und Icon |
| 11 | `start_url /play`, `scope /`, `standalone`; Anmeldung per Redirect | Integration (Manifest-Felder, bestehender Redirect-Test); **manuell:** Start vom Homescreen an- und abgemeldet, ohne Browserleiste |
| 12 | Service Worker: Navigation → Netz, sonst Offline-Seite; Texte aus `i18n.ts` | Unit `ServiceWorkerScriptTests`, Integration `/sw.js` und `offline.html`, Vitest Schlüssel; **manuell:** Flugmodus, Start und Neuladen, Deutsch und Englisch, „Erneut versuchen“ |
| 13 | `no-cache` für HTML/Manifest/SW, `immutable` für Assets, SW cacht kein App-Code, Version aus Offline-HTML | Integration Cache-Header, Unit Version; **manuell:** rc.N installiert, rc.N+1 ausrollen, App neu starten → neues Bundle (Versionsanzeige, AK-15) |
| 14 | `ScreenWakeLock` mit Neuanforderung bei Sichtbarkeit und bei Eingabe bis zum Erfolg | Vitest mit Fakes; **manuell:** Bildschirm-Timeout 30 s, im Spiel einmal tippen und warten; App wechseln → Timeout greift wieder; iPhone im Safari-Tab und als Homescreen-App |
| 15 | CI-Build-Arg `APP_VERSION` (Release-Tag, sonst kurzer Commit-Hash; bei PRs Head-Commit; Scan- und Push-Builds gleich) → `VITE_APP_VERSION` → Bundle; HUD unten rechts, `text-xs`, safe-area, über dem Overlay; lokal `dev` | Vitest `formatVersion`; **manuell:** `npm run dev` → `dev`; PR-Build zeigt den kurzen Head-Hash; im RC zeigt rc.N `X.Y.Z-rc.N`, nach Ausrollen von rc.N+1 und Neustart vom Homescreen `…-rc.N+1`; iPhone quer und hochkant; Overlay „getrennt“ lässt die Version sichtbar |
| 16 | Overlay: Tipp → sofort „lädt …“ (`reloadPage`), nach 8 s ohne Seitenwechsel Hinweis „Spiel neu öffnen“, Button wieder aktiv; Offline-Seite ebenso; Service Worker liefert die Assets der Offline-Seite zuerst aus dem Cache (auch bei 502–504 und 404) | Vitest `reload.test.ts`, Unit `ServiceWorkerScriptTests`; **manuell:** Homescreen-App (iPhone, Android) und Browser-Tab: Server stoppen, „getrennt“ abwarten, Server starten, Tipp → Spiel läuft ohne App-Neustart; App öffnen, während der Server gestoppt ist → Offline-Seite mit Stil und Text in beiden Sprachen; erscheint der Hinweis, Diagnose am iPhone |
| 17 | `ServerWatch` fragt bei „getrennt“ `/play` ab (sofort, alle 5 s, bei `online` und Sichtbarkeit), vergleicht das Entry-Script; anders → „Neue Version verfügbar. Bitte neu laden.“ mit „Neu laden“ | Vitest `update.test.ts`, `i18n.test.ts`; **manuell:** rc.N läuft in der App, rc.N+1 ausrollen → Hinweis in beiden Sprachen, Tipp → Versionsanzeige `…-rc.N+1` (AK-15) |

## Constitution-Check

- [x] **Server ist Autorität:** Das Sichtfeld beeinflusst keinen Spielstand; der Server übernimmt keine Bildschirmgröße.
- [x] **Typen nur aus `contracts/`:** keine Vertragsänderung.
- [x] **Offline-Fortschritt:** nicht betroffen („offline“ meint hier nur den Hinweis ohne Netz).
- [x] **Keine neuen Abhängigkeiten:** Service Worker handgeschrieben, Icons per `node:zlib`, Wake Lock ohne Polyfill,
  Server nur Framework (`System.Text.Json`, `GeneratedRegex`, SHA-256).
- [x] **KISS:** Service Worker cacht nur die Offline-Seite; Manifest statisch; ein ADR nur für das Sichtfeld (der
  Service Worker ist leicht umkehrbar).

## Risiken / Abweichungen

- **AK-2 bei niedriger Auflösung** : bis −26 % zwischen 801 und ~880 Gerätepixeln; nach oben nie
  mehr als +11 %.
- **iOS:** keine Installationsaufforderung (nur Teilen → „Zum Home-Bildschirm“); eigener Speicher und eigene Cookies
  der Homescreen-App (Service Worker und Anmeldung entstehen beim ersten Start neu); Wake Lock in Homescreen-Apps erst
  ab iOS 18.4 zuverlässig; ob Safari `start_url` statt der aktuellen Seite nutzt, im RC prüfen (Fallback: vom Spiel
  aus hinzufügen).
- **AK-14 auf iOS:** Nur Tippen oder Taste zählt als Geste, Ziehen nicht (WebKit-Bug 212117). Wer nur zieht, bekommt
  keine Sperre; der Bildschirm bleibt erst nach dem ersten Tippen an. Bewusst akzeptiert, kein „Tippen zum Starten“.
- **Ohne `devicePixelContentBoxSize`** (Safari) bei gebrochenem DPR (nur Seitenzoom am Mac) kann ein Gerätepixel
  Rundung leicht weichzeichnen.
- **Vite und absolute Kopf-Links** bei `base: "/play/"`: Build-Ausgabe prüfen (T9).
- **Zoom-Sperre auf `/play`**  geht zulasten der Barrierefreiheit im Spiel.
- **Fehler im Service Worker treffen alle Seiten des Hosts.** Deshalb minimal; `/sw.js` ist `no-cache` und wird bei
  jedem Seitenaufruf geprüft, ein Fix wirkt beim nächsten Aufruf.
- **HTTPS ist Pflicht** für Service Worker und Installation; die Testumgebung liefert es schon (Passkeys brauchen es auch). Am
  Handy daher im RC testen, nicht gegen den Dev-Rechner im LAN.
- **Fairness gegen manipulierte Clients:** Der Server schickt weiterhin alle Spieler; ein veränderter Client kann mehr
  zeigen. Unverändert gegenüber heute, Begrenzung erst mit Interest-Management (ADR 0010).
- **001 AK-6** (Ränder statt mehr Welt) ist abgelöst; Vermerk in der 001-Spec (T1).
- **Neuladen in der iOS-Homescreen-App** (RC rc.4: Tipp ohne Wirkung): Ursache offen. AK-16 sichert Rückmeldung und
  Hinweis, nicht die Ursache. Erscheint der Hinweis im RC, gezielt am iPhone diagnostizieren (Web-Inspektor); erst
  dann ggf. ein zweiter Navigationsversuch.
- **503 und späterer Wartungsmodus:** Der Service Worker macht aus 502–504 die Seite „Keine Verbindung – prüfe deine
  Internetverbindung“, `ServerWatch` wertet sie als „nicht erreichbar“. Eine Wartungsseite mit 503 würde verdeckt und
  wie ein Netzproblem aussehen. Die Wartungs-Spec muss das lösen (z. B. eigenes Kennzeichen, das der Service Worker
  durchreicht, oder Ankündigung per WebSocket → Protobuf).
- **Langsames Netz:** Nach 8 s erscheint der Hinweis, obwohl die Seite vielleicht noch lädt – harmlos, der Seitenwechsel
  ersetzt ihn.
- **Abfragelast:** Jeder getrennte, sichtbare Client lädt alle 5 s `/play` (~1 KB, statisch). Nach einem Deploy
  kurzzeitig gleichzeitig; bei der heutigen Spielerzahl unkritisch.
- **Update-Erkennung hängt an der Vite-Ausgabe** (ein Modul-Skript mit `src`). Ändert sie sich, fällt sie auf „gleich“
  zurück (kein falscher Hinweis); Vitest prüft die heutige Form.
