# Projektstruktur

Diese Seite beschreibt, **wo** etwas im Repo liegt und **warum**. Kurzversion im
[`README.md`](../README.md); die Begründung für die Feature-Systematik steht in
[ADR 0013](decisions/0013-projektstruktur.md).

## Grundprinzipien

- **Ordnung nach Feature, nicht nach Technik.** Alles, was zu einem Domain gehört, liegt zusammen —
  im Client unter `features/<name>/`, im Server unter `Features/<Name>/`. Pro Feature ein Name auf beiden
  Seiten (Server als Leitbild, im Client kleingeschrieben): `accounts`, `game`, `localization`, `pwa`.
- **Nur `Domain/` ist fest.** Reine Logik ohne Netz, Datenbank und ASP.NET ist das Leitbild und bekommt immer einen
  eigenen Ort. Die übrigen Ordner (`Application/`, `Infrastructure/`, `Presentation/`) gibt es nur, wenn das Feature
  sie wirklich trennt — kein Leer-Zeug, keine Ordner für eine einzelne Datei ohne eigene Rolle.
- **Abhängigkeiten zeigen in eine Richtung** (siehe [Abhängigkeitsrichtung](#abhängigkeitsrichtung)).
- **`shared` nur für echte Querschnittsthemen.** Etwas wandert erst dorthin, wenn es von mindestens zwei
  Features genutzt wird. Vorher bleibt es im Feature.
- **Verhalten vor Struktur.** Strukturänderungen sind `git mv` + Importpfade — kein Verhaltensumbau.
  Die Tests beweisen das.

## Top-Level

| Pfad | Inhalt |
|---|---|
| `specs/` | Features-Anforderungen: `spec.md` (WAS/WARUM), `plan.md`, `tasks.md` |
| `contracts/` | Einzige Typquelle: OpenAPI (HTTP), Protobuf (WebSocket); Typen werden generiert, nie gedoppelt |
| `docs/decisions/` | ADRs (0001–0015) |
| `client/` | TypeScript/React/Canvas-Spielclient (siehe unten) |
| `server/` | .NET: Spielserver, Admin, gemeinsame Datenschicht, Tests (siehe unten) |
| `deploy/`, `scripts/`, `.github/` | Betrieb (Compose), Release-Tooling, CI |
| `README.md`, `SECURITY.md`, `Dockerfile`, `buf.*`, `.editorconfig` | Repo-Wurzel bleibt bewusst klein |

## Client

```
client/
  index.html                 Einstieg Spiel (/play); lädt /src/app/main.tsx
  offline.html               Zweit-Einstieg (PWA-Offline-Seite); lädt /src/features/pwa/pages/offline.ts
  vite.config.ts             Mehrseiten-Build (index + offline), Tests (vitest), Alias @/ (aus tsconfig)
  vite.passkeys.config.ts    Feste Bundles für die Razor-Seiten: passkeys.js und language.js
                             (Einträge: features/accounts/api/passkeys.ts, features/localization/language.ts)
  oxlint.config.ts           Linter (ADR 0014), inkl. Importgrenzen zwischen Features (siehe unten)
  package.json               Skripte: dev/test/lint/build; build:landing mit src/styles/landing.css als
                             Tailwind-Eingabe; build:admin kopiert Bootstrap (scripts/admin-lib.mjs)
  src/
    app/                     main.tsx, App.tsx — Bootstrapping und Komposition
    features/
      game/                  Das Spiel selbst
        core/                constants.ts, vector.ts — Basis, von allen Unterordnern genutzt
        engine/              game.ts (Orchestrierung)
        state/               prediction.ts, interpolation.ts (Client-Vorschau des Serverstands)
        input/               input.ts, bindings.ts, wakeLock.ts
        render/              render.ts, viewport.ts, hud.ts
        network/             net.ts (WebSocket + Protobuf)
        chat/                Chat.tsx (Chatfenster), chatLogic.ts (reine Logik)
        menu/                Menu.tsx (Spielmenü), KeyBindings.tsx (Tastenbelegung), menuLogic.ts und keyBindingsLogic.ts (reine Logik)
        hud/                 Vitals.tsx (HP-/Mana-Balken im HUD, reine Funktionen vitalsText/fillPercent)
        combat/              targeting.ts (Zielauswahl und Angriffswunsch, reine Logik, 013), AttackButtons.tsx (Angriffs-Buttons und Hinweise im HUD, 013)
      accounts/api/          passkeys.ts — WebAuthn-Flow der Spielerseiten
      localization/          i18n.ts (Texte, Sprache) · language.ts (bestätigt bei Formularverlust)
      pwa/
        services/            pwa.ts (Service-Worker), update.ts (Server-Watch), reload.ts, version.ts
        pages/               offline.ts — Einstieg der Offline-Seite
    styles/                  global.css (Spiel+Offline), landing.css (Tailwind-Eingaben)
    generated/               Protobuf-Ausgabe von `buf generate` (nicht editieren)
    vite-env.d.ts            Ambient-Typen (bleibt auf src-Ebene)
```

Regeln:

- **Alias `@/` = `client/src/`** (`tsconfig.json` `paths`, Vite nutzt ihn über `resolve.tsconfigPaths`). Importe über
  Ordnergrenzen laufen über `@/…` (in `features/` verbietet oxlint `../`); nur Importe im selben Ordner bleiben relativ (`./x`). `generated/` nur über
  `@/generated/…`.
- **Importgrenzen** prüft oxlint (`no-restricted-imports`, `npm run lint`): Features importieren nie aus `app/`,
  `app/` importiert Features. Zwischen Features ist nur erlaubt, was die Tabelle unter
  [Abhängigkeitsrichtung](#abhängigkeitsrichtung) nennt. Neue Abhängigkeit = Tabelle und `oxlint.config.ts` anpassen.
- **Tailwind-Quellen:** `landing.css` scannt nur `**/*.cshtml` des Spielservers, kein C#. Das Admin nutzt Bootstrap ohne Tailwind (ADR 0007).
- **Spielnahe Features** (Chat, Kampf, Gegner, Slash-Commands …) kommen als Unterordner `features/game/<sub>/`, nicht als
  eigenes Top-Level-Feature. Bestehender Code wird nicht künstlich in Subs aufgeteilt.

## Server

```
server/
  BrowserGame.Data/          Geteilt von Spiel und Admin (ADR 0006): Entities, DbContexts, Migrations,
                             WebDefaults (Health-Probe, Security-Header, Rate-Limits), SmtpMailer + MailErrors (Resources/: IdentityErrors, MailErrors),
                             SqliteBackupService, PasskeySetup, Settings
  BrowserGame.Server/        Der Spielserver
    Program.cs               Bootstrap: Middleware-Reihenfolge, Registrierung nur über Add…/Use…-Extensions
    Hosting/                 Querschnitt des Hosts: FeaturePageRouteConvention (URLs stabil), PagesSetup (Razor-Optionen)
    Features/
      _ViewImports.cshtml, _ViewStart.cshtml, Index.cshtml   Razor-Vorgabe (RootDirectory=/Features); Index = Startseite /
      Shared/                _Layout, _Errors, _LanguageSwitch (Razor-Vorgabe)
      Accounts/
        AccountSetup.cs      DI-Anmeldung des Features (AddGameAccounts/AddGameEmail)
        Application/         Registrierung (RegistrationLinks), Passkey-Zeremonien, Konto-Mails
                             (GameEmailSender, AccountEmails), Konto-Links, PlayerNameMessages
        Domain/              PlayerNames (Namensregeln) + blocked-names.txt, AccountCredentials
        Infrastructure/      PlayerEmailSender (SMTP-Versand)
        Presentation/        Razor-Seiten (Account/…) - URLs bleiben /Account/…
      Game/
        GameSetup.cs         DI-Anmeldung (AddGame)
        Application/         GameLoop (Hosted Service), PlayerOutbox (Ausgang je Verbindung), GameSettings
        Domain/              World (Spieler, Eingaben), PlayerColors
        Infrastructure/      PositionStore (Persistenz der Positionen)
        Presentation/        GameEndpoint (WebSocket /ws), GameAccess (Origin-Prüfung)
        Chat/Domain/         ChatRules (Text), ChatRateLimiter (Tempo), ChatHistory (Verlauf)
        Chat/Application/    ChatSaveQueue (Warteschlange Tick-Thread → ChatStore)
        Chat/Infrastructure/ ChatStore (Verlauf laden, gebündelt schreiben, aufräumen)
        KeyBindings/Domain/  KeyBindingRules (Tastenbelegung prüfen, 002)
        KeyBindings/Infrastructure/ KeyBindingStore (Belegung am Account speichern, 002)
        Combat/Domain/       Combat (Kampfregeln, Trainings-Dummy, 013), Target, Projectile, AttackResult
      Localization/
        LocalizationSetup.cs DI und Middleware (AddGameLocalization/UseGameLocalization)
        Application/         GameLanguages, LanguageCookie, LanguageOnlyCultureProvider
        Presentation/        Language.cshtml (Umschalter)
        Resources/           Texts.cs + Texts(.de).resx  (ResourcesPath="" – siehe ADR 0013)
      Pwa/Application/       ServiceWorkerScript (+ service-worker.js, Endpoint /sw.js), PwaCaching
    Properties/, wwwroot/    launchSettings, gebaute Client-Ausgaben (manifest, icons, play/, css/)
  BrowserGame.Admin/         Eigener Admin-Dienst, eigene Pages/ (Shared/: Layouts, Partials _Errors/_Message, PageMessage, BootstrapValidationTagHelper); Auth/ (Totp, Invites, Audit); wwwroot/lib/ (Bootstrap, gebaut); wwwroot/css/admin.css (Layout, eingecheckt); wwwroot/js/theme.js (Farbschema, eingecheckt); Resources/ (AdminTexts.cs + AdminTexts.resx, neutral = Englisch)
  BrowserGame.Server.Tests/  Features/{Accounts,Game,Localization,Pwa}, Hosting/, Data/ (Tests von BrowserGame.Data) und TestSupport/
  BrowserGame.Admin.Tests/   Admin-Tests (bindet gemeinsame Helper aus Server.Tests/TestSupport/)
```

### Schichten

| Ordner | Inhalt |
|---|---|
| `Domain/` | Reine Logik: kein Netz, keine Datenbank, kein ASP.NET. Ohne Netzwerk und DB testbar (Constitution §7). |
| `Application/` | Abläufe und Dienste des Features (Hosted Services, Mails, Links). Nutzt `Domain/`. |
| `Infrastructure/` | Anbindung nach außen: Stores (Datenbank), SMTP. Nutzt `Application/` und `Domain/`. |
| `Presentation/` | Nur Razor-Seiten und Endpoints. Nutzt `Application/` und `Domain/`. |

Ein Ordner entsteht erst, wenn er eine eigene Rolle hat. Kleine Features bleiben flach (z. B. nur `Application/`): `Pwa` hält auch den Endpoint `/sw.js` dort, statt
einen `Presentation/`-Ordner für eine Datei anzulegen.

### Abhängigkeitsrichtung

Innerhalb eines Features: `Domain` kennt nichts außerhalb von `Domain`. `Application` → `Domain`.
`Infrastructure` und `Presentation` → `Application`/`Domain`. Nie umgekehrt, keine Zyklen.

Zwischen Features nur in eine Richtung, ohne Zyklen. Querschnitt-Features (Localization, Pwa) darf jedes Feature
nutzen, soweit die Tabelle es nennt; sie nutzen selbst keine Fach-Features. Der Querschnitt darf untereinander nutzen,
was die Tabelle erlaubt (Client: `pwa` → `localization`).

| Feature | Server: darf nutzen | Client: darf nutzen |
|---|---|---|
| `Accounts` / `accounts` | Game (`GameLoop` beim Abmelden/Löschen), Localization | — |
| `Game` / `game` | Localization (aktuell ungenutzt) | localization |
| `Localization` / `localization` (Querschnitt) | — | — |
| `Pwa` / `pwa` (Querschnitt) | — | localization |
| `app/` (nur Client) | — | alle Features |

### Regeln

- **URLs ändern sich nicht.** Die Feature-Route-Konvention (`Hosting/`) schneidet auf Razor-Seiten
  `<Feature>/Presentation/` aus der URL. Deshalb gilt weiter `/Account/Login`, `/Language`, `/`.
- **Razor-Seiten** liegen unter `Features/<Feature>/Presentation/`, Ausnahme `Features/Index.cshtml`; ein
  `Index.cshtml` in `Presentation/` gibt es nicht. Eindeutige Routen und diese Lage prüft `PageStructureTests`.
- **`asp-page` und `RedirectToPage`** zeigen auf den seitenrelativen Pfad, z. B.
  `asp-page="/Localization/Presentation/Language"` — nicht auf die kurze URL. Neuen Seiten: so.
- **Namespaces:** Schichten stehen nicht im Namespace, alle Typen eines Features liegen in
  `BrowserGame.Server.Features.<Feature>`. Ausnahmen: Razor-Seiten (`@namespace`/Ordnerpfad, z. B.
  `…Accounts.Presentation.Account`) und `Texts` (`…Localization.Resources`, der Ressourcen-Lookup braucht den Pfad).
- **Persistenz:** Entities, `DbContext` und Migrationen bleiben in `BrowserGame.Data` (ADR 0006). Im Feature liegen nur
  Stores (`Infrastructure/`), z. B. `PositionStore`.
- **Spielnahe Features** (Chat, Kampf, Gegner, Slash-Commands …) kommen als Unterordner `Features/Game/<Sub>/`, nicht als
  eigenes Top-Level-Feature. `Game` hostet Tick und `World`. Bestehender Code wird nicht künstlich in Subs aufgeteilt.
- **Neues Feature:** Ordner unter `Features/<Name>/` anlegen, nur die nötigen Schichten, Tests in
  `BrowserGame.Server.Tests/Features/…`. In `Program.cs` nur eine `Add…`-/`Use…`-Extension des Features
  (Razor-Seiten werden automatisch gefunden); die Middleware-Reihenfolge bleibt in `Program.cs` sichtbar.
  `contracts/` und `specs/` bleiben unberührt.

## Nicht mehr gültige Pfade

Vor ADR 0013 lagen Razor-Seiten in `server/BrowserGame.Server/Pages/`, Ressourcen in `Resources/`,
Client-Dateien zum Teil lose in `client/src/`. Plan-/Task-Dokumente in `specs/` nennen teils die alten
Pfade. Umgesetzte Specs behalten die alten Pfade (sie beschreiben den Stand zur Umsetzung); Specs im Status
`bereit` werden nachgezogen.
