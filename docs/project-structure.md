# Projektstruktur

Diese Seite beschreibt, **wo** etwas im Repo liegt und **warum**. Kurzversion im
[`README.md`](../README.md); die Begründung für die Feature-Systematik steht in
[ADR 0013](decisions/0013-projektstruktur.md).

## Grundprinzipien

- **Ordnung nach Feature, nicht nach Technik.** Alles, was zu einem Domain gehört, liegt zusammen —
  im Client unter `features/<name>/`, im Server unter `Features/<Name>/`.
- **Fester innerer Aufbau.** Jedes Feature hat dieselben Schichten; wer ein Feature kennt, kennt alle.
- **`shared` nur für echte Querschnittsthemen.** Etwas wandert erst dorthin, wenn es von mindestens zwei
  Features genutzt wird. Vorher bleibt es im Feature.
- **Verhalten vor Struktur.** Strukturänderungen sind `git mv` + Importpfade — kein Verhaltensumbau.
  Die Tests beweisen das (534 Server- und Admin-Tests, 189 Client-Tests).

## Top-Level

| Pfad | Inhalt |
|---|---|
| `specs/` | Features-Anforderungen: `spec.md` (WAS/WARUM), `plan.md`, `tasks.md` |
| `contracts/` | Einzige Typquelle: OpenAPI (HTTP), Protobuf (WebSocket); Typen werden generiert, nie gedoppelt |
| `docs/decisions/` | ADRs (0001–0013) |
| `client/` | TypeScript/React/Canvas-Spielclient (siehe unten) |
| `server/` | .NET: Spielserver, Admin, gemeinsame Datenschicht, Tests (siehe unten) |
| `deploy/`, `scripts/`, `.github/` | Betrieb (Compose), Release-Tooling, CI |
| `README.md`, `SECURITY.md`, `Dockerfile`, `buf.*`, `.editorconfig` | Repo-Wurzel bleibt bewusst klein |

## Client

```
client/
  index.html                 Einstieg Spiel (/play); lädt /src/app/main.tsx
  offline.html               Zweit-Einstieg (PWA-Offline-Seite); lädt /src/features/pwa/pages/offline.ts
  vite.config.ts             Mehrseiten-Build (index + offline), Tests (vitest)
  vite.passkeys.config.ts    Feste Bundles für die Razor-Seiten: passkeys.js und language.js
                             (Einträge: features/account/api/passkeys.ts, features/i18n/language.ts)
  package.json               Skripte: dev/test/lint/build; build:landing & build:admin mit
                             src/styles/landing.css bzw. src/styles/admin.css als Tailwind-Eingabe
  src/
    app/                     main.tsx, App.tsx — Bootstrapping und Komposition
    features/
      game/                  Das Spiel selbst
        engine/              game.ts (Orchestrierung), constants.ts, vector.ts
        state/               prediction.ts, interpolation.ts (Client-Vorschau des Serverstands)
        input/               input.ts, wakeLock.ts
        render/              render.ts, viewport.ts, hud.ts
        network/             net.ts (WebSocket + Protobuf)
      account/api/           passkeys.ts — WebAuthn-Flow der Spielerseiten
      i18n/                  i18n.ts (Texte, Sprache) · language.ts (bestätigt bei Formularverlust)
      pwa/
        services/            pwa.ts (Service-Worker), update.ts (Server-Watch), reload.ts, version.ts
        pages/               offline.ts — Einstieg der Offline-Seite
    styles/                  global.css (Spiel+Offline), landing.css, admin.css (Tailwind-Eingaben)
    generated/               Protobuf-Ausgabe von `buf generate` (nicht editieren)
    vite-env.d.ts            Ambient-Typen (bleibt auf src-Ebene)
```

Regeln: `features/<Feature>/` verweist intern auf andere Features nur über deren `features/…`-Pfade;
`app/` importiert Features, Features importieren sich nicht über `app/`. Kein Loop.

## Server

```
server/
  BrowserGame.Data/          Geteilt von Spiel und Admin (ADR 0006): DbContexts, Migrations,
                             WebDefaults (Health-Probe, Security-Header, Rate-Limits), SmtpMailer,
                             SqliteBackupService, PasskeySetup, Settings
  BrowserGame.Server/        Der Spielserver
    Program.cs               Bootstrap: Middleware, DI, Razor-Optionen (RootDirectory=/Features)
    Features/
      Accounts/
        AccountSetup.cs      DI-Anmeldung des Features (AddGameAccounts/AddGameEmail)
        Application/         Use-Cases: Registrierung (RegistrationLinks), Passkey-Zeremonien,
                             Zugangsdaten, Konto-Mails, Konto-Links
        Domain/              PlayerNames (Namensregeln) + blocked-names.txt
        Infrastructure/      EmailSenders (SMTP-Abstraktionen)
        Presentation/        Razor-Seiten (Account/…) - URLs bleiben /Account/…
      Game/
        Presentation/        GameEndpoint (WebSocket /ws), GameAccess (Origin-Prüfung), PlayerColors
        Application/         GameLoop (Hosted Service), GameSettings
        Domain/              World (Spieler, Eingaben)
        Infrastructure/      PositionStore (Persistenz der Positionen)
      Localization/
        Presentation/        Language.cshtml (Umschalter)
        Services/            GameLanguages, LanguageCookie, LanguageOnlyCultureProvider
        Resources/           Texts.cs + Texts(.de).resx  (ResourcesPath="" – siehe ADR 0013)
      Pwa/Services/          ServiceWorkerScript (+ service-worker.js), PwaCaching
    Features/Shared/         Razor-Infrastruktur: _ViewImports, _ViewStart, _Layout, _Errors,
                             _LanguageSwitch, Index.cshtml (Startseite, URL /)
    Shared/
      Pages/                 FeaturePageRouteConvention – hält URLs wie /Account/Login stabil
    Properties/, wwwroot/    launchSettings, gebaute Client-Ausgaben (manifest, icons, play/, css/)
  BrowserGame.Admin/         Eigener Admin-Dienst, eigene Pages/; Auth/ (Totp, Invites, Audit)
  BrowserGame.Server.Tests/  Features/{Accounts,Game,Localization,Pwa,Data} + TestSupport/
  BrowserGame.Admin.Tests/   Admin-Tests (bindet gemeinsame Helper aus Server.Tests/TestSupport/)
```

Regeln:

- **URLs ändern sich nicht.** Die Feature-Route-Konvention (`Shared/Pages/`) schneidet auf Razor-Seiten
  `<Feature>/Presentation/` aus der URL. Deshalb gilt weiter `/Account/Login`, `/Language`, `/`.
- **`asp-page` und `RedirectToPage`** zeigen auf den seitenrelativen Pfad, z. B.
  `asp-page="/Localization/Presentation/Language"` — nicht auf die kurze URL. Neuen Seiten: so.
- **Neues Feature:** Ordner unter `Features/<Name>/` anlegen, interne Schichten wie oben, Tests in
  `BrowserGame.Server.Tests/Features/…`, Verweis in `Program.cs` nur über die `Add…`-Extension
  (Razor-Seiten werden automatisch gefunden). `contracts/` und `specs/` bleiben unberührt.
- **Konventionen:** Layer-Benennung folgt der Tabelle oben. Ein Feature mit nur einer Schicht lässt die
  anderen Ordner einfach weg (kein Leer-Zeug).

## Nicht mehr gültige Pfade

Vor ADR 0013 lagen Razor-Seiten in `server/BrowserGame.Server/Pages/`, Ressourcen in `Resources/`,
Client-Dateien zum Teil lose in `client/src/`. Plan-/Task-Dokumente in `specs/` nennen teils die alten
Pfade — sie beschreiben den Stand zum Zeitpunkt der Umsetzung und werden nicht rückwirkend geändert.
