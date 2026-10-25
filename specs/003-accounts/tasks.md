# 003 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung → CI/Betrieb.

## Verträge
- [x] **T1** (AK-12, AK-20, AK-22): `game.proto` um `PlayerState.away` und `ServerMessage.kicked` (`Kicked.Reason`)
  erweitern; `buf lint` und `buf breaking` gegen `main` müssen grün sein.

## Server – Daten
- [x] **T2**: Pakete (Identity.EntityFrameworkCore, EF Core Sqlite/Npgsql/Design, DataProtection.EntityFrameworkCore),
  `dotnet-ef` als lokales Tool. `GameDbContext` mit `Account`, `PlayerPosition`, Data-Protection-Keys;
  `SqliteGameDbContext` und `PostgresGameDbContext`, Provider-Auswahl per Konfiguration.
- [x] **T3**: Erste Migration für beide Provider (`scripts/add-migration.sh Initial`). Migration beim Start,
  WAL bei SQLite, `SqliteBackupService` (`VACUUM INTO`).

## Server – Anmeldung
- [x] **T4** (AK-5, AK-6, AK-7b): `PlayerNames` (Normalisieren, Regeln, Sperrliste) + Unit-Tests.
- [x] **T5** (AK-3, AK-9–11, AK-15): Identity- und Cookie-Konfiguration, Lockout, Token-Lebensdauer, Security-Stamp,
  Rate-Limiter, Forwarded-Header, Passkey-Optionen.
- [x] **T6** (AK-2, AK-3, AK-14): `IEmailSender<Account>` – `SmtpEmailSender` (MailKit) und `LogEmailSender`
  (Development), deutsche Texte, Links aus `App:PublicUrl`.
  _Wird durch 004 T4 ersetzt: SMTP-Einstellungen aus der DB (Admin-Interface) statt `Mail:*`._
- [x] **T7** (AK-1–3, AK-7, AK-9, AK-13): Razor Pages `Register` (Passwort-Weg), `Login`, `ConfirmEmail`,
  `ResendConfirmation`, `Logout`; Startseite mit Anmelde-Links; `/play` mit `RequireAuthorization`.
- [ ] **T8** (AK-14, AK-15): `ForgotPassword`, `ResetPassword`.
- [ ] **T9** (AK-4, AK-13b, AK-13c, AK-16): Passkeys bei Registrierung, Anmeldung und `Manage`; `CanRemoveCredential`
  + Unit-Test; Passwort setzen; `Delete` mit Bestätigung. Dazu `client/src/account/passkeys.ts` als fester
  Build `wwwroot/js/passkeys.js`.

## Server – Spiel
- [ ] **T10** (AK-8, AK-17, AK-18): `/ws` nur angemeldet + bestätigt, Origin-Prüfung; Spieler-ID = Account-ID,
  Name aus dem Account, Position laden, stabile Farbe.
- [ ] **T11** (AK-12, AK-20–23): `World` mit `away`/Linger (+ Unit-Tests), `GameLoop` mit Übernahme, Rauswurf
  (`Kicked`) und `ForceLeave` (aus Logout, Reset, Löschen).
- [ ] **T12** (AK-18, AK-19): `PositionStore` – gebündeltes Speichern alle 5 s, beim Entfernen und beim Herunterfahren.

## Client
- [ ] **T13** (AK-12, AK-19b, AK-20, AK-22): `kicked`-Overlay, `away`-Figuren entsättigt, HUD mit Koordinaten und Link „Mein Account“.

## Tests / Prüfung
- [ ] **T14**: Integrationstests laut Abdeckungstabelle (Test-Mailsender, verkürzter Linger), lauffähig gegen
  SQLite und PostgreSQL (Provider per Umgebungsvariable).
  _AK-1, AK-2, AK-3, AK-7, AK-9 und AK-13 samt Test-Infrastruktur (`CapturingEmailSender`, `FormClient`,
  `AccountTestFlows`) schon mit T7 erledigt._
- [ ] **T15**: CI-Job `server-postgres` (Service-Container, Digest gepinnt) und Prüfung auf pending model changes für
  beide Kontexte; in `ci-ok.needs`.

## Betrieb
- [ ] **T16**: `deploy/compose.yaml` (Volume `/data`, Datenbank, URL, Passkey-Domain, Forwarded-Header),
  README-Abschnitt „Konfiguration“ mit allen Variablen.
  _Großteils mit 004 T13 erledigt (Vorlage `deploy/compose.template.yaml` mit Volume, URL, Passkey-Domain,
  Forwarded-Header; README „Betrieb“). Offen: Feinschliff, sobald 003 fertig ist._ Hinweise fürs Homelab: Volume ins Backup aufnehmen,
  Netcup-Postfach + SPF/DKIM.
- [ ] **T17**: Manuelle Prüfung auf dem Pi:
  - AK-4, AK-13b und AK-13c mit echten Passkeys (Handy + Laptop)
  - Mail-Empfang (Bestätigung, Reset)
  - AK-12 mit zwei Geräten, AK-20 und AK-21 per Tab schließen und neu laden

  Danach alle AKs abhaken und Spec-Status auf `umgesetzt`.
