# ADR 0013 – Projektstruktur nach Features

**Status:** akzeptiert (2026-10-30)

## Kontext

Client und Spielserver sind gewachsen (Accounts, Game, Localization, Pwa). Der Code lag schon weitgehend
feature-orientiert, aber unregelmäßig: Im Client lagen `pwa.ts`, `update.ts`, `reload.ts`, `offline.ts`,
`version.ts` und drei CSS-Dateien lose in `src/`; schon vorhanden waren nur `src/game/` und `src/account/`.
Im Spielserver standen die Feature-Gruppen (`Accounts/`, `Game/`, `Localization/`, `Pwa/`) neben einem
separaten `Pages/`-Baum für Razor und einem `Resources/`-Baum für die Textressourcen.

Ziel ist eine gleiche Arbeitsweise auf beiden Seiten: Ordnung nach **Feature**, mit einheitlichen Ordnernamen
(`Domain/` immer, `Application/`, `Infrastructure/`, `Presentation/` nur bei Bedarf). Details: [`docs/project-structure.md`](../project-structure.md).
Das Verhalten ändert sich nicht.

## Entscheidung

### Client: `features/` plus `app/` und `styles/`

```
client/src/
  app/                       Einstiegspunkte (main.tsx, App.tsx)
  features/
    game/                    core/ · engine/ · state/ · input/ · render/ · network/
    accounts/                api/ (passkeys.ts, fester Vite-Build passkeys.js)
    localization/            i18n.ts (Wörterbuch) · language.ts (Umschalter für Razor-Seiten)
    pwa/                     services/ (pwa.ts, update.ts, reload.ts, version.ts) · pages/ (offline.ts)
  styles/                    global.css · landing.css · admin.css
  generated/                 Protobuf-Typen aus `buf generate` (Quelle bleibt contracts/)
```

Tests liegen weiter neben der getesteten Datei (`*.test.ts`, Include-Muster `src/**/*.test.ts` bleibt).
Ein `shared/`-Ordner gibt es bewusst nicht: Er entsteht erst, wenn Code von mindestens zwei Features genutzt
wird. `game/core/` hält die Basis des Spiels (`constants.ts`, `vector.ts`), damit `engine/` nicht im Kreis
mit `input/`, `render/` und `state/` hängt. Der Pfad-Alias `@/` zeigt auf `client/src/`; ESLint erzwingt die
Importgrenzen zwischen Features.

### Server: `Features/<Name>/` mit Schichten nach Bedarf

```
server/BrowserGame.Server/
  Program.cs                 Bootstrap; Registrierung nur über Add…/Use…-Extensions
  Hosting/                   FeaturePageRouteConvention, PagesSetup
  Features/
    _ViewImports.cshtml · _ViewStart.cshtml · Index.cshtml · Shared/   Razor-Vorgabe
    Accounts/                AccountSetup.cs · Application/ · Domain/ · Infrastructure/ · Presentation/Account/
    Game/                    GameSetup.cs · Application/ · Domain/ · Infrastructure/ · Presentation/
    Localization/            LocalizationSetup.cs · Application/ · Presentation/ · Resources/ (Texts.cs + Texts(.de).resx)
    Pwa/                     Application/
server/BrowserGame.Data/     unverändert, gemeinsame Daten- und Server-Ebene (ADR 0006)
server/BrowserGame.Admin/    unverändert, eigener Dienst
```

Schichten: `Domain/` ist reine Logik und das Leitbild; `Application/`, `Infrastructure/` und `Presentation/` gibt es nur,
wenn ein Feature sie wirklich trennt. Abhängigkeiten zeigen nach innen (Infrastructure/Presentation → Application →
Domain), zwischen Features nur in eine Richtung (heute: Accounts → Game); Localization und Pwa sind Querschnitt.
Die Schichten stehen nicht im Namespace (`BrowserGame.Server.Features.<Feature>`); Ausnahmen sind Razor-Seiten und
`Texts`. Spielnahe Features (Chat, Kampf, Gegner …) kommen unter `Features/Game/<Sub>/` bzw. `features/game/<sub>/`.
Die Tabelle steht in [`docs/project-structure.md`](../project-structure.md).

Die Tests wurden umsortiert: `BrowserGame.Server.Tests` gruppiert nach
`Features/{Accounts,Game,Localization,Pwa}`, `Hosting/` und `Data/` (Tests des Data-Projekts) plus `TestSupport/`.

### Drei Besonderheiten bei ASP.NET Core

1. **Razor-Seiten:** `RazorPagesOptions.RootDirectory = "/Features"`. Die Seiten liegen jetzt unter
   `Features/<Feature>/Presentation/…`, die **URLs bleiben** `/Account/Login` usw. Dafür schneidet die
   `FeaturePageRouteConvention` (in `Hosting/`) `<Feature>/Presentation/` aus der Route, die aus dem
   Dateipfad entsteht.

2. **`asp-page` und Redirects:** Beides löst nach dem seitenrelativen Pfad auf, nicht nach der URL. Die
   Attribute zeigen deshalb auf z. B. `/Localization/Presentation/Language`. Was im Browser sichtbar bleibt,
   ist trotzdem die kurze URL (`action="/Language"`), weil die Konvention die Template kürzt.
   Die Index-Seite liegt direkt unter `Features/`, damit Razor sie wie bisher unter `/` findet; `_ViewImports` und `_ViewStart` liegen aus demselben Grund dort.

3. **Textressourcen:** `Texts.cs` und `Texts(.de).resx` liegen in `Features/Localization/Resources/`.
   Die `ResourceManagerStringLocalizerFactory` baut den Basisnamen aus `typeof(Texts)`; deshalb bleibt
   `ResourcesPath` **leer** (""), sonst verdoppelt sich der Pfad. Der statische Basisname (`Texts.BaseName`)
   und der Manifestname der Ressource decken sich jetzt exakt. `blocked-names.txt` und `service-worker.js`
   wandern mit; die `LogicalName`-Attribute im csproj folgen den Feature-Ordnern (gelesen wird über
   `LogicalName`, nicht über den Pfad).

Nicht umgezogen: `WebDefaults` (Health-Probe, Security-Header, Rate-Limit-Politiken) bleibt
im **Data-Projekt**, weil Spiel **und** Admin es gemeinsam nutzen (ADR 0006, zwei getrennte Dienste).
Ein `Shared/`-Ordner nur im Spielserver würde eine Kopie oder eine unnötige Abhängigkeit Admin → Server
bedeuten.

## Alternativen

Nur eine moderate Variante war angedacht: Feature-Gruppen ohne Layer, Razor-Seiten blieben in `Pages/`.
Das habe ich in der Abstimmung verworfen: gleich mehr Aufwand, aber zwei weitere Sonderfälle statt einer
klaren Systematik.

## Nachweis

- `dotnet test` (SQLite): der komplette Testlauf ist grün, auch nach dem Umzug.
- `npm run typecheck`, `lint`, `test`, `build`: grün.
- Lokaler Smoke-Test beim manuellen Serverstart: Startseite, Spielansicht `/play`, Verbindung `/ws`,
  Service Worker `/sw.js` und die Umschalter-Formulare verhalten sich wie vor dem Umzug.
