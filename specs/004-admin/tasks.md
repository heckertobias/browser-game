# 004 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Datenschicht → Admin → Spiel-Anbindung → Tests → Docker/CI/Betrieb.

## Datenschicht
- [x] **T1**: Bibliothek `BrowserGame.Data` anlegen; `Data/*` aus dem Server dorthin verschieben (Namespace
  `BrowserGame.Data`), Server und Tests umstellen. Alle bisherigen Tests müssen grün bleiben.
- [x] **T2** (AK-7, AK-8, AK-10): `Settings`-Tabelle, `SmtpSettingsStore` mit verschlüsseltem Passwort (Zweck `Settings.Smtp`).
- [x] **T3** (AK-1, AK-4e, AK-12): `AdminDbContext` (Identity v3 mit Präfix `Admin`, `AdminInvites`, `AdminAuditEntries`),
  eigene Migrations-Historie; SQLite-/Postgres-Kontexte. (Sperrkonflikte: Microsoft.Data.Sqlite wiederholt bei `SQLITE_BUSY` bis zum Command-Timeout, kein eigenes `busy_timeout` nötig.) Admin-Indizes umbenannt (datenbankweit eindeutig).
  `scripts/add-migration.sh` für beide Kontexte; Migrationen neu erzeugen (game + admin, je SQLite + Postgres).
- [x] **T4** (AK-9, AK-10, AK-11): Mailversand nach `BrowserGame.Data`: `SmtpEmailSender` liest pro Mail aus dem Store,
  fehlende Einstellungen → Warnung + verwerfen; `TestMailAsync` mit deutschen Fehlermeldungen. `Mail:*`-Konfiguration
  und Startabbruch aus 003 T6 entfernen.

## Admin
- [x] **T5**: Projekt `BrowserGame.Admin` (Razor Pages, Layout im Pixel-Akzent-Stil), Testprojekt, Migration beim Start
  (Game + Admin), Sicherheits-Header, `/health`.
- [x] **T6** (AK-2–4, AK-4d): Identity für `AdminUser`, Admin-Cookies mit eigenem Schutzzweck (8 h, SameSite=Strict), Lockout,
  Rate-Limiter, `RequireAuthorization` für alles außer Login/Invite.
- [x] **T7** (AK-1, AK-4b, AK-4c, AK-4e): Einladungen (`AdminInvites`, Hash, 48 h, einmalig), Setup-Einladung beim
  ersten Start (Link ins Log), Seite `Invite`: Passwort + Authenticator (QRCoder) + Wiederherstellungscodes;
  Passkey-Weg (gemeinsames `passkeys.js`).
- [x] **T8** (AK-4b, AK-4c): `Login` mit Passkey oder Passwort + TOTP/Wiederherstellungscode, `Logout`, `Account`
  (eigene Passkeys/2FA verwalten).
- [x] **T9** (AK-7–9): Seite `Smtp` (Formular, „gesetzt“, Testmail).
- [x] **T10** (AK-4e, AK-4f, AK-12): Seiten `Admins` (einladen, entfernen, letzter bleibt) und `Audit`; `AuditLog` an
  allen Änderungen.

## Tests
- [x] **T11**: Integrationstests laut Abdeckungstabelle (inkl. TOTP-Berechnung im Test, SMTP-Testserver,
  gemeinsame Test-DB für AK-10), gegen SQLite und PostgreSQL.

## Docker, CI, Betrieb
- [x] **T12**: Dockerfile mit Zielen `game` und `admin`; CI-`docker`-Job als Matrix, zweites Image
  `browser-game-admin`; `release.sh` und Release-Notes für beide Images.
- [ ] **T13**: `deploy/compose.yaml` um `browsergame-admin` (gleiches Volume, `ADMIN_VIRTUAL_HOST`), README
  („Admin-Interface“, erster Admin über den Link im Log). Hinweise fürs Homelab: interner Hostname, Volume ins Backup.
- [ ] **T14**: Manuelle Prüfung auf dem Pi:
  - Erster Admin über den Log-Link
  - Passkey-Anmeldung
  - SMTP mit dem Netcup-Postfach eintragen, Testmail
  - Spiel-Container stoppen → Admin geht weiter (AK-5)
  - Admin von außen nicht erreichbar (AK-6)

  Danach alle AKs abhaken und Spec-Status auf `umgesetzt`.
