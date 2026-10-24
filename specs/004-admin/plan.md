# 004 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidung: [ADR 0006](../../docs/decisions/0006-admin-als-eigener-dienst.md)
(eigener Dienst, gemeinsame DB); baut auf [ADR 0004](../../docs/decisions/0004-datenbank-sqlite-oder-postgresql.md)
und [ADR 0005](../../docs/decisions/0005-anmeldung-identity-passkeys-mail.md) auf.

## Überblick

```
server/
  BrowserGame.Data/          Bibliothek: GameDbContext, AdminDbContext, Migrationen, Einstellungen, Mailversand
  BrowserGame.Server/        Spiel (liest SMTP-Einstellungen aus der DB)
  BrowserGame.Admin/         Admin-Interface (Razor Pages), eigener Container
  *.Tests/                   je Projekt

/data/game.db  ◄── Spiel (Container 1)
               ◄── Admin (Container 2, nur intern erreichbar)
```

## Verträge

Keine Änderung. Admin und Spiel tauschen nichts über das Netz aus (ADR 0006), die Admin-Seiten sind serverseitige
Formulare.

## Datenschicht (`BrowserGame.Data`)

- **Umzug:** `Data/*` aus `BrowserGame.Server` mit Namespace `BrowserGame.Data`. Die Migrationen von 003 werden neu
  erzeugt; es gibt noch keine Produktiv-Datenbank, v0.1.0 hatte keine.
- **`GameDbContext`** (wie 003) **+ `Settings`**: Schlüssel/Wert-Tabelle (`Key` PK, `Value`, `UpdatedAt`). Darin liegt
  zuerst `Smtp` als JSON; das Passwort ist darin verschlüsselt (Data Protection, Zweck `Settings.Smtp`).
- **`AdminDbContext : IdentityDbContext<AdminUser>`**:
  - Tabellen mit Präfix `Admin`: `AdminUsers`, `AdminUserPasskeys`, `AdminUserTokens` (Authenticator-Schlüssel,
    Wiederherstellungscodes) usw. Identity-Schema Version 3.
  - `AdminInvites`: `Id`, `TokenHash` (SHA-256), `CreatedBy`, `CreatedAt`, `ExpiresAt`, `UsedAt`. Der Einrichtungslink
    ist eine Einladung mit `CreatedBy = "setup"`.
  - `AdminAuditEntries`: `Id`, `At`, `Admin`, `Action`, `Details`.
- **Migrations-Historien getrennt:** `__EFMigrationsHistory` (Spiel) und `__EFMigrationsHistory_Admin`.
- **Provider wie ADR 0004:** je Kontext ein SQLite- und ein Postgres-Kontext.
  `scripts/add-migration.sh <Name> [game|admin]` erzeugt alle nötigen Migrationen.
- **Migration beim Start:**
  - Das Spiel migriert `GameDbContext`.
  - Das Admin migriert `GameDbContext` und `AdminDbContext`, damit es auch ohne vorherigen Spielstart läuft (AK-5).
  - EF Core 10 sperrt parallele Migrationen.
- **`SmtpSettingsStore`**:
  - `GetAsync()` liefert die entschlüsselten Einstellungen oder `null`, `SaveAsync(settings, keepPassword)` speichert sie (AK-8).
  - Gelesen wird **bei jedem Versand** direkt aus der DB, ohne Cache. Damit gilt eine Änderung ab der nächsten Mail (AK-10).
- **Mailversand** (aus 003 hierher verschoben, beide Dienste nutzen ihn):
  - `GameEmailSender` mit den Texten bleibt.
  - `SmtpEmailSender` holt sich die Einstellungen pro Mail aus dem `SmtpSettingsStore`. Fehlen sie, schreibt er eine
    Warnung ins Log und verwirft die Mail (AK-11).
  - `Mail:*` aus der Konfiguration und der Startabbruch ohne SMTP (003 T6) entfallen.
  - `TestMailAsync(to)` liefert Erfolg oder einen verständlichen Fehler. MailKit-Ausnahmen werden übersetzt,
    z. B. `AuthenticationException` → „Anmeldung am Mailserver fehlgeschlagen“ (AK-9).

## Admin (`BrowserGame.Admin`)

- **Identity** für `AdminUser`:
  - Identitys Standard-Cookies, aber mit eigenem Data-Protection-Zweck `BrowserGame.Admin.Cookies` (ein Spieler-Cookie
    ist im Admin nicht entschlüsselbar) und eigenen Namen `bg.admin.*`; HttpOnly, Secure, SameSite=Strict, 8 Stunden fest (AK-4d).
  - Lockout und Rate-Limiting wie im Spiel (AK-4).
  - `RequireConfirmedEmail` entfällt, Admins kommen über Einladungen.
  - Passkey-`ServerDomain` aus `Passkeys:ServerDomain` (Admin-Host).
- **Anmeldung (AK-4b):**
  - Passkey (`PasskeySignInAsync`) **oder** Passwort mit Pflicht-2FA: `PasswordSignInAsync`, dann
    `RequiresTwoFactor` → TOTP oder Wiederherstellungscode.
  - Einen Admin mit Passwort, aber ohne 2FA kann es nicht geben, denn die Einrichtung erzwingt den Schritt.
- **Einrichtung/Einladung** (`/Invite?token=…`, AK-1, AK-4e):
  - Token prüfen (Hash, nicht abgelaufen, nicht benutzt) und Name + E-Mail eingeben.
  - Danach Passkey anlegen **oder** Passwort setzen und Authenticator einrichten.
  - Der Authenticator wird per QR-Code als SVG über QRCoder eingebunden; der Schlüssel steht zusätzlich als Text da.
  - Zum Abschluss werden die 10 Wiederherstellungscodes einmal angezeigt (AK-4c) und das Token als benutzt markiert.
- **Erster Start (AK-1):** Gibt es keinen Admin und keine gültige Setup-Einladung, wird eine erzeugt. Der Link landet
  **einmal** im Log (`App:PublicUrl` des Admins), mit einer Gültigkeit von 48 h; beim nächsten Start ohne Admin wird
  ein neuer erzeugt.
- **Seiten** (alle bis auf Login/Invite nur angemeldet, AK-3):
  - `Index`: Übersicht.
  - `Smtp`: Formular mit Server, Port, Verschlüsselung (Auto/SSL/STARTTLS/keine), Benutzer, Passwort, Absender
    (Adresse + Name). Das Passwortfeld ist leer; daneben steht „gesetzt“, solange eins gespeichert ist (AK-7, AK-8).
    Dazu „Testmail senden“ (AK-9).
  - `Admins`: Liste, „Einladen“ (Link anzeigen, optional per Mail), „Entfernen“, das den letzten Admin verweigert (AK-4e, AK-4f).
  - `Audit`: Protokoll, neueste zuerst (AK-12).
  - `Account`: eigene Passkeys und 2FA, neue Wiederherstellungscodes.
- **Protokoll (AK-12):** `AuditLog.WriteAsync(admin, action, details)` bei SMTP-Änderung, Testmail, Einladung,
  Einladung eingelöst und Admin entfernt. Bei den SMTP-Details stehen nur die geänderten Feldnamen, niemals Werte des Passworts.
- **Sicherheits-Header** wie im Spiel, CSP ohne `connect-src` zu externen Zielen. `/health` für den Docker-Healthcheck.
- **Passkey-JS:** dasselbe `passkeys.js` wie das Spiel (003 T9). Der Client-Build legt es in beide `wwwroot/js`.

## Spiel (`BrowserGame.Server`)

- Nutzt `BrowserGame.Data` und bekommt den Mailversand aus der DB-Konfiguration. Sonst ändert sich nichts.

## Docker, CI, Deployment

- **Dockerfile:** ein Multi-Stage-Dockerfile mit zwei Zielen, `--target game` und `--target admin`. Die Build-Stages
  werden geteilt, die Laufzeit-Images sind getrennt (beide chiseled, non-root, `HEALTHCHECK`).
- **CI:**
  - `docker`-Job als Matrix über `game` und `admin`, jeweils mit beiden Architekturen und Trivy.
  - Push nach `ghcr.io/heckertobias/browser-game` bzw. `…/browser-game-admin`, mit denselben Tags (`main`, `sha-…`,
    `X.Y.Z`, `latest`).
  - Die Release-Notes nennen beide Images. `release.sh` prüft beide Images und setzt beide Versionen in der Compose.
- **`deploy/compose.yaml`:**
  - Neuer Dienst `browsergame-admin`: gleiches Volume `/data`, `VIRTUAL_HOST: ${ADMIN_VIRTUAL_HOST}`,
    `App__PublicUrl`, `Passkeys__ServerDomain`, read-only, `cap_drop`.
  - Kein veröffentlichter Port; nur intern über den Proxy. Ob der Hostname nur intern auflöst, ist Sache des
    Homelabs (AK-6).
- **Dependabot:** Das Admin-Projekt liegt unter `/server` und ist dort schon abgedeckt.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Setup-Einladung beim ersten Start, Link im Log, einmalig | Integration: Start ohne Admin → Log enthält Link; einlösen → Admin angelegt; zweites Einlösen → abgelehnt |
| 2 | getrennte Identity-Modelle und Cookie-Schemas | Integration: Spieler-Daten im Admin-Login → abgelehnt; Admin-Cookie am Spiel → nicht angemeldet |
| 3 | `RequireAuthorization` außer Login/Invite | Integration: jede Admin-Seite anonym → Redirect zum Login |
| 4 | Lockout + Rate-Limiter | Integration: 6. Fehlversuch gesperrt, 11. POST/Minute → 429 |
| 4b | Einrichtung erzwingt Passkey oder Passwort + TOTP | Integration (Passwort-Weg): Login ohne TOTP-Code → kein Zugang; mit Code (aus dem Schlüssel berechnet) → Zugang. Passkey-Weg **manuell** |
| 4c | Wiederherstellungscodes | Integration: Code einmal gültig, zweites Mal abgelehnt |
| 4d | Cookie 8 h fest | Integration: `Set-Cookie`-Ablauf ≈ 8 h |
| 4e | Einladungen (Hash, 48 h, einmalig) | Unit: Ablauf/Einmaligkeit; Integration: Einladung → neuer Admin |
| 4f | letzter Admin bleibt | Integration: Entfernen des einzigen Admins → abgelehnt; mit zweitem → klappt |
| 5 | getrennte Dienste, eigene Migration | Integration: Admin-Factory ohne laufendes Spiel funktioniert; **manuell**: Spiel-Container stoppen → Admin geht |
| 6 | nur interner Hostname | **manuell** (Homelab) |
| 7, 8 | `SmtpSettingsStore`, Passwort verschlüsselt, leer = unverändert | Unit/Integration: speichern, Passwort nicht im HTML, nicht im Klartext in der DB, leeres Feld behält es |
| 9 | `TestMailAsync` mit übersetzten Fehlern | Integration gegen einen lokalen SMTP-Testserver (MailKit-eigener Testserver bzw. kleiner Fake), falsche Zugangsdaten → deutsche Meldung |
| 10 | Lesen pro Versand | Integration: Einstellungen im Admin ändern → nächste Spiel-Mail nutzt sie (gemeinsame Test-DB) |
| 11 | fehlende Einstellungen → Warnung, Mail verworfen | Integration: Registrierung ohne SMTP klappt, Log-Warnung |
| 12 | `AuditLog` | Integration: Änderung → Eintrag ohne Passwort, auf der Audit-Seite sichtbar |

Alle Integrationstests laufen gegen SQLite und PostgreSQL.

## Constitution-Check

- [x] **Server ist Autorität:** Das Admin schreibt nur Einstellungen, Spielzustand fasst es nicht an.
- [x] **Typen nur aus `contracts/`:** Es gibt keine Netz-Schnittstelle zwischen Admin und Spiel.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Neue Abhängigkeiten:** QRCoder (QR-Code für die Authenticator-Einrichtung). Alles andere ist schon da.
- [ ] **KISS:** zweiter Dienst und zweites Image, bewusst entschieden (ADR 0006).

## Risiken / Abweichungen

- **003 ändert sich:** Mail-Konfiguration aus der DB statt `.env`. 003 T6 wird hier überarbeitet, die 003-`plan.md`
  wird angepasst.
- **Zwei Prozesse auf einer SQLite-Datei:** WAL und `busy_timeout` (5 s) gegen Sperrkonflikte. Schreiblast vom Admin
  ist minimal.
- **Erster Admin:** Wer Zugriff auf das Container-Log hat, kann sich zum ersten Admin machen. Das ist gewollt
  (Betreiber), wird aber im README deutlich gemacht.
- **TOTP-Tests** brauchen einen Code-Generator im Test. Ich berechne RFC 6238 im Test selbst, ohne zusätzliches Paket.
