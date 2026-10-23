# 001 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Browser ── GET /      → Razor-Startseite (Button „Spielen“)
        ── GET /play  → React/Canvas-Client (statisch aus wwwroot/play)
        ── WS  /ws    → binäre Protobuf-Nachrichten, Server-Tick 20 Hz
```

Ein ASP.NET-Core-Prozess liefert alles aus. Der Spielzustand liegt nur im Speicher; es gibt keine Datenbank
(Speichern ist in der Spec ausdrücklich Out of Scope).

## Verträge

Neu: `contracts/proto/game.proto`. Kein HTTP-API in 001, `openapi/` bleibt leer.

```proto
syntax = "proto3";
package browsergame.v1;
option csharp_namespace = "BrowserGame.Contracts.V1";

// Client → Server
message ClientMessage {
  oneof payload { Input input = 1; }
}

// Gewünschte Laufrichtung für genau einen Server-Tick.
// WASD und Halten-und-Folgen ergeben beide einen Richtungsvektor.
message Input {
  uint32 seq = 1;   // fortlaufend pro Verbindung
  float dir_x = 2;  // Länge > 1 wird vom Server auf 1 gekürzt
  float dir_y = 3;
}

// Server → Client
message ServerMessage {
  oneof payload {
    Welcome welcome = 1;
    Snapshot snapshot = 2;
  }
}

message Welcome {
  string player_id = 1;
  string name = 2;
  float speed = 3;          // Welteinheiten pro Sekunde
  uint32 tick_rate_hz = 4;
}

message Snapshot {
  uint32 tick = 1;
  uint32 last_processed_seq = 2;  // pro Empfänger: letzte vom Server verarbeitete Eingabe
  repeated PlayerState players = 3;
}

message PlayerState {
  string id = 1;
  string name = 2;
  float x = 3;
  float y = 4;
  uint32 color = 5;  // 0xRRGGBB
}
```

**Codegen:**
- C#: `Grpc.Tools` im Server-csproj, `<Protobuf Include="../../contracts/proto/*.proto" GrpcServices="None" />`
  (nur Messages, keine gRPC-Services).
- TS: `buf generate` mit `@bufbuild/protoc-gen-es` (lokales Plugin aus `client/node_modules`) nach
  `client/src/generated/`. Das Verzeichnis ist durch `.gitignore` bereits ausgeschlossen; `npm run gen` läuft vor
  `dev`/`build`/`test`.
- `buf.yaml` liegt im Repo-Root, `buf lint` läuft in der CI.

## Server

`server/BrowserGame.Server` (ASP.NET Core, .NET 10 LTS) und `server/BrowserGame.Server.Tests` (xUnit).

- **`World`** (reine Spiellogik, kein Netzwerk, keine DB, deterministisch testbar):
  - `AddPlayer()` / `RemovePlayer(id)`, Spawn zufällig in einem kleinen Radius um (0,0).
  - `EnqueueInput(id, Input)`: Die Warteschlange ist pro Spieler auf 10 Einträge begrenzt, ältere fallen weg.
  - `Step()`: Pro Spieler wird **genau eine** Eingabe verbraucht, die Richtung auf Länge ≤ 1 gekürzt und um
    `speed / tickRate` bewegt. Ist die Warteschlange leer, gilt die letzte Richtung weiter.
  - Es gibt keine Weltgrenzen (AK-7).
- **`GameLoop`** (`BackgroundService`, `PeriodicTimer` 50 ms): `World.Step()`, danach Snapshot an alle Verbindungen,
  jeweils mit dem eigenen `last_processed_seq`. Bei ≤ 50 Spielern gibt es noch kein Interest Management.
- **`/ws`-Endpoint:**
  - Beim Verbinden `AddPlayer` und `Welcome` senden. Eingehende Frames werden als `ClientMessage` geparst und
    über `EnqueueInput` eingereiht.
  - Frames > 1 KB oder unparsbare Nachrichten trennen die Verbindung.
  - Bei Close/Fehler `RemovePlayer` (AK-4).
  - Thread-Sicherheit: Eingaben laufen über einen `Channel` in den Tick-Thread; nur dieser verändert `World`.
- **Anonyme Spieler:** zufälliger Fantasy-Name aus Silbenlisten und zufällige Farbe.
- **Razor Page `/`:** Titel, kurzer Text und daisyUI-Button „Spielen“ → `/play` (AK-1).
  Tailwind/daisyUI-CSS wird beim Client-Build mit erzeugt und von Razor mitbenutzt (siehe ADR 0001).
- **`/play`:** statische Dateien aus `wwwroot/play` (Vite-Build) mit SPA-Fallback auf `index.html`.
- **`/health`:** `MapHealthChecks` für den Docker-`HEALTHCHECK`.

## Client

`client/` (Vite, React, TypeScript, Tailwind + daisyUI, Vitest).

- **Rendering (AK-6, AK-7):**
  - Logische Auflösung **640×360** (siehe [ADR 0003](../../docs/decisions/0003-logische-aufloesung.md)) als eine Konstante.
  - Das Canvas wird intern mit 640×360 × Skalierungsfaktor gerendert. Der Faktor ist die größte ganze Zahl, die ins
    Fenster passt; ist das Fenster kleiner als 640×360, wird fraktional verkleinert. `imageSmoothingEnabled = false`.
    Der Rest bekommt Letterbox mit dunklen Rändern.
  - Die Kamera ist auf die eigene (vorhergesagte) Position zentriert. Im Hintergrund liegt ein Gitter mit 32 Einheiten
    Abstand und dickerer Linie alle 256 Einheiten, damit man Bewegung ohne Grenzen sieht.
  - Figuren sind 16×16 farbige Quadrate mit Namen darüber.
- **Eingabe (AK-2, AK-2b):**
  - `actions.ts`: Aktionen `moveUp | moveDown | moveLeft | moveRight` mit Standard-Belegung
    `KeyW/KeyS/KeyA/KeyD` (`KeyboardEvent.code`, damit layoutunabhängig). Das ist die Grundlage für 002.
  - Pointer Events (Maus und Touch einheitlich, `touch-action: none` auf dem Canvas): Solange gedrückt, ist die
    Richtung „Zeiger in Weltkoordinaten minus eigene Position“. Innerhalb von 4 Einheiten Abstand wird nicht gelaufen
    (kein Zittern auf dem Zielpunkt). Loslassen, `pointercancel` oder ein Fokusverlust stoppen die Figur.
  - Ist mindestens eine Bewegungstaste gedrückt, hat die Tastatur Vorrang vor dem Zeiger.
  - Daraus entsteht ein Richtungsvektor, normiert auf Länge ≤ 1.
- **Netzwerk und Prediction (AK-3, AK-5):**
  - Pro Fixed-Step (50 ms, wie der Server) wird ein `Input` mit `seq` gesendet und lokal mit derselben Formel
    angewandt; der Eingabepuffer speichert unbestätigte Eingaben.
  - Beim Snapshot wird die eigene Position auf den Server-Wert gesetzt. Die Eingaben mit `seq > last_processed_seq`
    werden erneut angewandt, die bestätigten verworfen. Hätte der Client unerlaubt schneller vorhergesagt, landet er
    so auf der Server-Position (AK-5).
  - Andere Spieler werden mit ~100 ms Verzögerung zwischen den letzten beiden Snapshots interpoliert.
  - Spieler, die nicht mehr im Snapshot stehen, werden entfernt (AK-4).
- **Seite:** `/play` zeigt Vollbild-Canvas und ein kleines HUD (eigener Name, verbunden/getrennt) im Pixel-Akzent-Stil.

## Docker

Multi-Stage-`Dockerfile` im Repo-Root:
1. `node`: `npm ci`, `npm run gen`, `npm run build` → `client/dist`.
2. `dotnet/sdk`: `dotnet publish` mit Client-Build in `wwwroot/play`.
3. `dotnet/aspnet` (chiseled, non-root) als Laufzeit-Image, Port 8080, `HEALTHCHECK` auf `/health`.

Basis-Images sind per Digest gepinnt und werden von Dependabot aktualisiert.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| AK-1 | Razor `/` mit Button → `/play` lädt Client, Figur erscheint nach `Welcome` | Integrationstest (`WebApplicationFactory`): `/` enthält Link auf `/play`, `/play` liefert 200; manuell im Browser |
| AK-2 | WASD → Richtungsvektor, normiert | Vitest: Tasten → Vektor (diagonal Länge 1); xUnit `World`: Strecke pro Tick = `speed/tickRate`; manuell |
| AK-2b | Pointer gedrückt → Richtung zum Zeiger, Loslassen → stopp | Vitest: Zeiger-Richtung, Totzone, Stopp bei `pointerup`/`pointercancel`; manuell mit Maus und Touch (DevTools-Emulation oder Handy) |
| AK-3 | 20-Hz-Snapshots an alle, Interpolation | Integrationstest: 2 WebSocket-Clients, B sieht neue Position von A < 1 s; manuell mit 2 Tabs |
| AK-4 | Close → `RemovePlayer` → fehlt im nächsten Snapshot | Integrationstest: A schließt, B's Snapshot ohne A innerhalb 1 s |
| AK-5 | Server kürzt Vektoren, verbraucht max. 1 Eingabe pro Tick, Client übernimmt Server-Position | xUnit: Vektor (10,0) und 50 Eingaben in einem Tick bewegen nicht weiter als `speed/tickRate`; Vitest: Reconciliation korrigiert eine „zu schnelle“ Vorhersage |
| AK-6 | Kamera zentriert, fixe logische Auflösung, Letterbox | Vitest: Skalierungsfaktor/Letterbox für 1280×720, 1920×1080, 3840×2160, 1000×1000; manuell bei mehreren Fenstergrößen |
| AK-7 | Keine Grenzen, Gitter-Hintergrund | xUnit: Bewegung bis (100 000, −100 000) ohne Begrenzung; manuell |

Die manuellen Prüfungen werden in `tasks.md` bei T15 mit Datum abgehakt.

## Constitution-Check

- [x] Server ist Autorität: Der Client schickt nur eine Richtung; Position, Geschwindigkeit und Tick-Takt bestimmt der Server.
- [x] Typen nur aus `contracts/`: C# und TS werden aus `game.proto` generiert.
- [x] Offline-Fortschritt: nicht relevant in 001.
- [x] Neue Abhängigkeiten, alle begründet:
  - `Google.Protobuf` und `Grpc.Tools` für C#-Codegen
  - `@bufbuild/protobuf` und `@bufbuild/protoc-gen-es` für TS-Codegen
  - Vite, React, Tailwind und daisyUI gemäß ADR 0001
  - xUnit, `Microsoft.AspNetCore.Mvc.Testing` und Vitest für Tests

## Risiken / Abweichungen

- **Kein Persistenz-Durchstich** (Constitution §6: Client → Server → Persistenz): Die Spec schließt Speichern aus.
  Der Durchstich Client → Server → alle Clients ist trotzdem vollständig. Persistenz kommt mit der ersten Spec,
  die sie braucht.
- **Prediction-Abweichung:** Kommt eine Eingabe zu spät, bewegt der Server mit der letzten Richtung weiter und der
  Client korrigiert leicht. Für 001 ist ein kleines Ruckeln akzeptabel; Glätten der Korrektur folgt bei Bedarf.
- **Lokale Umgebung:** Das .NET SDK ist auf dem Dev-Rechner noch nicht installiert (`brew install dotnet`).
- **Auto-Merge löst keine CI auf `main` aus** (siehe ADR 0002). Das ist erst relevant, sobald CD Images baut (T12).
