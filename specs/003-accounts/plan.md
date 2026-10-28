# 003 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidungen: [ADR 0004](../../docs/decisions/0004-datenbank-sqlite-oder-postgresql.md)
(Datenbank), [ADR 0005](../../docs/decisions/0005-anmeldung-identity-passkeys-mail.md) (Anmeldung, Mail)

## Überblick

```
Browser ── /Account/*  Razor Pages (Registrieren, Anmelden, Bestätigen, Passwort vergessen, Account verwalten)
        ── /play       nur angemeldet + E-Mail bestätigt, sonst → /Account/Login
        ── /ws         gleiches Cookie + Origin-Prüfung; Spieler = Account
Server  ── ASP.NET Core Identity (EF Core) ── SQLite | PostgreSQL (per Konfiguration, Bibliothek BrowserGame.Data aus 004)
        ── GameLoop: Nachlauf 20 s, Übernahme/Rauswurf, Positionen gebündelt speichern
        ── SMTP (eigenes Postfach) bzw. Log in der Entwicklung
```

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel:

```proto
message PlayerState {
  // … bestehende Felder 1–5
  // Spieler hat die Verbindung verloren und wartet noch (AK-20) – anders einfärben.
  bool away = 6;
}

message ServerMessage {
  oneof payload {
    Welcome welcome = 1;
    Snapshot snapshot = 2;
    // Verbindung wurde vom Server beendet (z. B. Anmeldung auf anderem Gerät, AK-12).
    Kicked kicked = 3;
  }
}

message Kicked {
  enum Reason {
    REASON_UNSPECIFIED = 0;
    REASON_SIGNED_IN_ELSEWHERE = 1;
    REASON_SIGNED_OUT = 2; // Abmelden, Passwort zurückgesetzt, Account gelöscht
  }
  Reason reason = 1;
}
```

**OpenAPI:** keine Änderung. Alle Account-Abläufe sind serverseitige Razor-Formulare, Passkey-Optionen werden ins
HTML gerendert (ADR 0005). Es entsteht keine JSON-API, deren Typen der Client teilen müsste.

## Server

### Daten (`Data/`)
- `Account : IdentityUser` – `UserName` = Spielername (normalisiert, z. B. „Tobias“), `Email` eindeutig.
  Identity-Schema **Version 3** (enthält die Passkey-Tabelle).
- `PlayerPosition` (`AccountId` PK/FK, `X`, `Y`, `UpdatedAt`) – Cascade-Delete mit dem Account.
- `DataProtectionKeys` (Cookies/Tokens überstehen Neustarts).
- `GameDbContext : IdentityDbContext<Account>`; abgeleitet `SqliteGameDbContext` und `PostgresGameDbContext` nur
  für die getrennten Migrationssätze (`Data/Migrations/Sqlite`, `Data/Migrations/Postgres`).
- Registrierung: `Database:Provider` = `Sqlite` (Standard) | `Postgres`, `ConnectionStrings:Game`.
  `AddDbContext<GameDbContext, SqliteGameDbContext>()` bzw. Postgres-Variante.
- Start: `Database.MigrateAsync()`. Bei SQLite einmalig `PRAGMA journal_mode=WAL`.
- `SqliteBackupService` (nur bei SQLite): täglich um 03:50 `VACUUM INTO '/data/backup/game.db'`, damit das
  nächtliche Backup (04:00) eine konsistente Kopie bekommt.
- `dotnet-ef` als lokales Tool (`server/dotnet-tools.json`), Migrationen per Skript `scripts/add-migration.sh <Name>`
  für beide Provider.

### Identity & Anmeldung (`Accounts/`)
- `AddIdentityCore<Account>().AddSignInManager().AddDefaultTokenProviders().AddEntityFrameworkStores<GameDbContext>()`,
  Cookie-Auth:
  - `RequireUniqueEmail`, `AllowedUserNameCharacters` = A–Z und a–z, `SignIn.RequireConfirmedEmail = true` (AK-3).
  - Passwort: Länge ≥ 10, keine Pflicht-Zeichenklassen.
  - Lockout: 5 Fehlversuche → 5 Minuten Sperre (AK-10, pro Account).
  - Cookie `bg.auth`: HttpOnly, Secure (außer Development), SameSite=Lax, 30 Tage, `SlidingExpiration = false`,
    immer persistent (AK-11).
  - Security-Stamp-Prüfung alle 1 min. Passwort-Reset, Abmelden und Löschen beenden damit andere Sitzungen (AK-15);
    zusätzlich wird eine laufende Spiel-Verbindung sofort beendet (siehe GameLoop).
  - Reset-Link: 3 Stunden, nur einmal nutzbar (Security-Stamp ändert sich). Registrierungslink: 30 Minuten
    (einstellbar, siehe Passkeys/Registrierung), faktisch einmal nutzbar, weil die Adresse danach vergeben ist.
  - Reset-Mail nur an Accounts mit bestätigter E-Mail; die Antwort bleibt für alle gleich (AK-14).
  - Ein erfolgreicher Reset hebt eine Lockout-Sperre auf und setzt die Fehlversuche zurück (AK-15).
- `IdentityPasskeyOptions.ServerDomain` aus `Passkeys:ServerDomain`; erlaubter Origin aus `App:PublicUrl`.
- **Rate-Limiting** (ASP.NET `RateLimiter`, pro Client-IP) für Anmelden, Registrieren, Passwort vergessen,
  Passwort zurücksetzen und Bestätigung erneut senden: 10 Anfragen pro Minute (AK-10). Hinter nginx-proxy werden die Forwarded-Header ausgewertet
  (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, Container nur im Proxy-Netz).
- **Namen** (`PlayerNames`, reine Logik):
  - `Normalize`: trimmen, prüfen auf `^[A-Za-z]{3,16}$`, dann erster Buchstabe groß, Rest klein (AK-5, AK-6).
  - Sperrliste `blocked-names.txt` (eingebettete Ressource, ganze Namen, Vergleich nach Normalisierung, AK-7b).
  - Eindeutigkeit über Identitys `NormalizedUserName`-Index (AK-7).
- **E-Mail:** kommt aus [004](../004-admin/plan.md). Die SMTP-Einstellungen stehen in der DB und werden im
  Admin-Interface gepflegt; der `SmtpEmailSender` liest sie pro Mail. Fehlen sie, wird die Mail verworfen und eine
  Warnung geloggt (004 AK-11). Texte und `AccountEmails` (Links aus `App:PublicUrl`) bleiben wie hier beschrieben.

### Seiten (`Pages/Account/`, daisyUI im Pixel-Akzent-Stil wie die Startseite)
| Seite | Zweck | AK |
|---|---|---|
| `Register` | Schritt 1: nur E-Mail, Antwort immer gleich; Mail mit Link (neu) bzw. „Du hast schon einen Account“ (vorhanden) | 2, 3, 9 |
| `Register/Complete` (Name frei) | Schritt 2 über den Link: Spielername, dann Passwort **oder** Passkey; legt den Account bestätigt an und meldet an | 2, 4–7b |
| `Login` | E-Mail + Passwort oder „Mit Passkey anmelden“ (Request-Options ohne Nutzer); einheitliche Fehlermeldung | 9, 10 |
| `ForgotPassword`, `ResetPassword` | Reset-Link anfordern (immer gleiche Antwort) bzw. neues Passwort setzen | 14, 15 |
| `Manage` | Passkeys auflisten/hinzufügen/entfernen, Passwort setzen (falls keins), Abmelden, Link zu Löschen | 13–13c |
| `Delete` | Bestätigung per Passwort oder Passkey, dann Account + Daten löschen, abmelden | 16 |
| `Logout` (POST) | abmelden, Spiel-Figur sofort entfernen | 13, 22 |

- Regel „mindestens eine Anmeldeart“ (AK-13c) als reine Funktion `CanRemoveCredential(hasPassword, passkeyCount)`.
- Passkey-JS: `client/src/account/passkeys.ts` (eigener Vite-Build als festes `wwwroot/js/passkeys.js`). Nutzt
  `PublicKeyCredential.parseCreationOptionsFromJSON` bzw. `parseRequestOptionsFromJSON` und schreibt das Ergebnis
  in ein verstecktes Formularfeld.
- **Passkeys (T9):**
  - Registrierung (T9c, ersetzt den Passwort-Weg aus T7): Schritt 1 fragt nur die E-Mail ab, die Antwort ist für jede
    Adresse gleich. Der Link trägt E-Mail und Ablaufzeit, signiert per Data Protection (`ToTimeLimitedDataProtector`); in der DB
    entsteht noch nichts. Gültigkeit per Einstellung `Registration:LinkLifetimeMinutes`, Standard 30. Mail und
    Seite nennen die Dauer („Der Link ist 30 Minuten gültig“), keine Uhrzeit (Zeitzone unbekannt). Einladungslinks
    setzen später nur eine längere Dauer.
    Schritt 2 legt den Account mit Name und Passwort oder Passkey an, bestätigt (`EmailConfirmed = true`), setzt
    `Account.Language` (006 AK-7) und meldet persistent an. Existiert die Adresse inzwischen, ist der Link wertlos;
    damit ist er faktisch nur einmal nutzbar. Es gibt keine unbestätigten Accounts mehr; `ConfirmEmail` und
    `ResendConfirmation` entfallen. Später sollen Einladungslinks denselben Schritt 2 nutzen.
  - Passkey-Anmeldung ist wie die Passwort-Anmeldung persistent (AK-11). `PasskeySignInAsync` meldet nur
    nicht-persistent an, darum einzeln: Assertion, `CanSignInAsync`, Lockout, Sign-Count speichern,
    `SignInAsync(isPersistent: true)`, Sprach-Cookie.
  - Der Zustand einer Passkey-Zeremonie liegt 5 min in einem Cookie. Fehlt er oder passt er nicht, gibt es eine
    Fehlermeldung statt einer Ausnahme (`PasskeyCeremonies`, Grund ins Log).
  - Login rendert die Request-Options immer, auch nach einem Fehlversuch.
  - Passkey-Liste: „Passkey {n}, angelegt am {yyyy-MM-dd}“ (UTC), ohne JS.
  - Unbestätigte Accounts gibt es seit T9c nicht mehr; ein Passkey-Login scheitert dann mit der allgemeinen Meldung.
  - `Manage` entfernt nur Passkeys und setzt ein Passwort, wenn keins da ist. Passwort ändern oder entfernen gehört
    nicht dazu. Einen Passkey zu entfernen beendet keine anderen Sitzungen.
  - `CanRemoveCredential` heißt: Nach dem Entfernen bleibt mindestens eine Anmeldeart.
- Startseite: „Spielen“ führt zu `/play`; zusätzlich „Anmelden“ / „Registrieren“ bzw. Name + „Mein Account“.
- `/play`: Fallback-Routen mit `RequireAuthorization()`. Nicht angemeldet → Redirect zu `/Account/Login?returnUrl=/play`
  (AK-1). Die Assets unter `/play/assets` bleiben öffentlich.

### Spiel (`Game/`)
- `/ws`:
  - Nur bei angemeldetem Nutzer mit bestätigter E-Mail, sonst 401.
  - `Origin` muss `App:PublicUrl` entsprechen, sonst 403 (in Development auch `http://localhost:5173`).
  - Vor dem Beitritt wird die gespeicherte Position aus der DB geladen; fehlt sie, ist der Spawn am Startpunkt (AK-17, AK-18).
- `GameLoop`, Spieler-ID = Account-ID:
  - **Beitritt** eines Accounts, der schon in der Welt ist:
    - Die alte Verbindung bekommt `Kicked(SIGNED_IN_ELSEWHERE)` und wird geschlossen.
    - Die neue übernimmt die **bestehende** Figur (AK-12). Wartet die Figur gerade, wird sie wieder aktiv (AK-21).
  - **Verbindungsende** (Close/Abbruch): Figur wird `away` und bekommt keine Eingaben mehr. Nach `LingerSeconds = 20`
    wird sie entfernt und ihre Position gespeichert (AK-20, AK-23).
  - **`ForceLeave(accountId)`** (aus Logout, Reset, Löschen): sofort entfernen, Verbindung mit
    `Kicked(SIGNED_OUT)` schließen (AK-22, AK-15, AK-16).
  - Im Snapshot trägt `PlayerState.away` den Wartezustand; die Farbe ist pro Account stabil (aus der ID abgeleitet).
- `World` bleibt ohne Netzwerk und Datenbank:
  - `AddPlayer(id, name, color, x, y)` statt Zufallsspawn.
  - `SetAway(id, tick)` / `ClearAway(id)`; `Step()` bewegt wartende Figuren nicht.
  - `RemoveExpiredAway(tick, lingerTicks)` liefert die entfernten Figuren zurück.
- **Positionen speichern** (`PositionStore`):
  - Der Tick-Thread legt alle 5 s alle Positionen sowie jede entfernte Figur in einen Channel.
  - Ein `BackgroundService` schreibt sie gebündelt per Upsert (AK-19: höchstens ~5 s Verlust).
  - Beim Herunterfahren (`StopAsync`) wird alles geschrieben.
- `GameSettings`: `LingerSeconds = 20`, `PositionSaveIntervalSeconds = 5`, in Tests per Konfiguration verkürzbar.

## Client

- `net.ts`:
  - `kicked` → Status `kicked` mit Grund.
  - Overlay „Auf einem anderen Gerät angemeldet“ mit Button „Hier weiterspielen“ (neu laden) bzw. Weiterleitung zur
    Anmeldung bei `SIGNED_OUT`.
- `render.ts`: `away`-Figuren entsättigt und halbtransparent (AK-20).
- HUD: Spielername aus dem Account, **eigene Koordinaten** (gerundet, aus der vorhergesagten Position, AK-19b), Link
  „Mein Account“ (bis das Menü aus 002 kommt).
- Verbindung bricht ohne `Kicked` ab → wie bisher „getrennt“ mit „Neu verbinden“ (innerhalb von 20 s nahtlos, AK-21).

## Deployment

`deploy/compose.yaml`:
- Volume `browsergame-data:/data` (das Root-Dateisystem bleibt read-only).
- Umgebung:
  - `Database__Provider: Sqlite`, `ConnectionStrings__Game: Data Source=/data/game.db`.
  - `App__PublicUrl: https://${VIRTUAL_HOST}`, `Passkeys__ServerDomain: ${VIRTUAL_HOST}`.
  - `ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"`.
  - Mail: keine Umgebungsvariablen mehr, Einstellungen im Admin-Interface (004).
- Betrieb, nicht in diesem Repo: Volume ins Backup aufnehmen, Mail-Postfach anlegen.

## CI

- Job `server`: Tests mit SQLite (wie bisher).
- Neuer Job `server-postgres`: dieselben Integrationstests mit `Database__Provider=Postgres` gegen den Service-Container
  `postgres:17` (Image per Digest gepinnt), in `ci-ok.needs`.
- Prüfung, dass die Migrationen beider Provider zum Modell passen
  (`dotnet ef migrations has-pending-model-changes` für beide Kontexte).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `/play` mit `RequireAuthorization` → Login-Redirect | Integration: anonym → 302 auf Login |
| 2 | Register in zwei Schritten | Integration mit Test-Mailsender: Mail mit Link; Schritt 2 mit Passwort und mit Passkey → Account bestätigt, angemeldet |
| 3 | Account erst in Schritt 2, befristeter Link | Integration: nach Schritt 1 kein Account, Name frei; abgelaufener oder benutzter Link abgewiesen; vorhandene Adresse → gleiche Seite, Hinweis-Mail |
| 4 | Passkey-Registrierung ohne Passwort | Integration mit Software-Authenticator (Test-Helfer, kein Paket); **manuell** mit echten Geräten (T17) |
| 5, 6, 7b | `PlayerNames` | Unit: Längen, Zeichen, Schreibweise, Sperrliste |
| 7 | eindeutiger `NormalizedUserName` | Integration: zweite Registrierung mit „tobias“ abgelehnt |
| 8 | Welcome/Snapshot mit Account-Namen | Integration (WS): Name im Snapshot |
| 9 | einheitliche Fehlermeldung | Integration: falsches Passwort und unbekannte Mail → identische Antwort |
| 10 | Lockout + Rate-Limiter | Integration: 6. Fehlversuch gesperrt; 11. Anfrage/Minute → 429 |
| 11 | persistentes Cookie, 30 Tage fest | Integration: `Set-Cookie` mit Expires ≈ 30 Tage |
| 12 | Übernahme + `Kicked(SIGNED_IN_ELSEWHERE)` | Integration (WS): zweite Verbindung → erste bekommt Kicked, eine Figur |
| 13 | Logout | Integration: danach `/play` → Login |
| 13b, 13c | Manage-Seite, `CanRemoveCredential` | Unit für die Regel; Integration mit gespeicherten Passkeys und Software-Authenticator; **manuell** mit echten Geräten |
| 14 | ForgotPassword | Integration: bekannte und unbekannte Mail → gleiche Antwort, nur bekannte bekommt Mail |
| 15 | Reset-Token einmalig/befristet, Security-Stamp | Integration: zweite Nutzung scheitert; alte Sitzung danach abgemeldet |
| 16 | Delete mit Bestätigung, Cascade | Integration (Passwort und Passkey): Account weg, Name wieder registrierbar |
| 17, 18 | Position laden/speichern | Integration: erster Beitritt am Startpunkt; Bewegung, gehen, wiederkommen → gleiche Stelle |
| 19 | gebündeltes Speichern + Flush beim Stop | Integration: neuer Server auf derselben DB-Datei → Position ≤ 5 s alt |
| 19b | HUD zeigt gerundete eigene Position | Vitest: Formatierung; **manuell**: gehen, wiederkommen, gleiche Koordinaten |
| 20, 23 | `away` + Entfernen nach Linger | Unit (`World`); Integration mit verkürztem Linger: `away=true`, dann weg |
| 21 | Übernahme wartender Figur | Integration: innerhalb Linger neu verbinden → gleiche ID/Position, `away=false` |
| 22 | `ForceLeave` beim Logout | Integration: nach Logout sofort aus Snapshot verschwunden |

Alle Integrationstests laufen gegen SQLite **und** PostgreSQL (CI).

## Constitution-Check

- [x] **Server ist Autorität:** Identität kommt aus dem Cookie, nicht vom Client. Positionen speichert nur der Server.
- [x] **Typen nur aus `contracts/`:** Protobuf erweitert. Es gibt keine neue JSON-API, also kein OpenAPI.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Neue Abhängigkeiten, alle begründet:**
  - Identity.EntityFrameworkCore (Anmeldung)
  - EF Core Sqlite und Npgsql (ADR 0004), EF Core Design (Migrationen)
  - DataProtection.EntityFrameworkCore (Schlüssel)
  - MailKit (SMTP)
- [ ] **KISS:** bewusste Abweichung durch zwei Datenbank-Provider (ADR 0004).

## Risiken / Abweichungen

- **Passkeys** brauchen HTTPS und die richtige Domain. Getestet wird manuell in der Testumgebung
  und lokal über `localhost`.
- **Zwei Migrationssätze** können auseinanderlaufen. Die CI prüft deshalb pending model changes für beide und testet beide.
- **Forwarded-Header** werden ohne Proxy-Allowlist vertraut. Das ist sicher nur, solange der Container keinen Port
  veröffentlicht (nur Proxy-Netz). Deshalb steht das im README und als Kommentar in der Compose.
- **Mails** können im Spam landen. SPF/DKIM für die Mail-Domain gehören zur Einrichtung des Postfachs (Betrieb).
- **Unbestätigte Accounts aus der Zeit vor T9c** (nur Testumgebung) kommen nicht mehr weiter; vor dem Release löschen.
- **Datenbank-Datei** auf der SD-Karte: WAL mit gebündelten Schreibzugriffen alle 5 s hält die Schreiblast gering.
