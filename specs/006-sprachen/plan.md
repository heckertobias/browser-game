# 006 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidung: [ADR 0008](../../docs/decisions/0008-lokalisierung.md) (Lokalisierung)

## Überblick

```
Browser ── Spieler-Seiten (Razor)  Sprache je Anfrage: Culture-Cookie → Accept-Language (nur erster Eintrag) → en
        ── Umschalter im Layout    POST /Language → Cookie setzen (+ Account-Sprache, falls angemeldet) → zurück
        ── /play (Client)          Sprache aus dem Culture-Cookie, sonst Browser; im Spiel fest
Server  ── Texts.resx / Texts.de.resx (Seiten, Namensregeln, Mails), IdentityErrors(.de).resx in BrowserGame.Data
        ── Account.Language        bei Registrierung gesetzt, beim Umschalten aktualisiert, beim Login ins Cookie
        ── Mails                   in der Sprache des Accounts
Admin   ── bis 005 fest Deutsch (gleicher Mechanismus, eine Sprache)
```

## Verträge

**Protobuf:** keine Änderung. Der Server schickt keine Texte, nur Zustände (z. B. `Kicked.Reason`); der Client
übersetzt sie selbst.

**OpenAPI:** keine Änderung. Der Umschalter ist ein Razor-Formular, keine JSON-API.

## Server

### Sprachen (`Localization/GameLanguages.cs`, Server)
- `Supported = ["en", "de"]`, `Default = "en"`.
- `Normalize(string? code)` → unterstützter Code oder `Default` (für Cookie, Formular, Datenbank).

### Daten (`BrowserGame.Data`)
- `Account.Language` (`string`, max. 8 Zeichen, Pflicht). C#-Standardwert `"de"`.
- Migration `AddAccountLanguage` für beide Provider (`scripts/add-migration.sh AddAccountLanguage`). Bestehende
  Accounts bekommen `"de"` (`defaultValue: "de"` in beiden Migrationen prüfen bzw. setzen): Sie haben sich auf
  Deutsch registriert.

### Lokalisierung (`Program.cs`, `Localization/`)
- `AddLocalization(o => o.ResourcesPath = "Resources")`.
- `AddRazorPages().AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(Texts)))`:
  `ErrorMessage` in `[Required(...)]` ist dann ein Schlüssel in `Texts.resx`.
- `UseRequestLocalization` direkt nach `UseSecurityHeaders`:
  - `SupportedCultures` = `SupportedUICultures` = `GameLanguages.Supported`, `DefaultRequestCulture = "en"`.
  - Provider genau in dieser Reihenfolge: `CookieRequestCultureProvider`, dann `AcceptLanguageHeaderRequestCultureProvider`
    mit `MaximumAcceptLanguageHeaderValuesToTry = 1` (nur die bevorzugte Sprache zählt, AK-1, AK-2). Kein
    Query-String-Provider.
  - `de-AT`, `de-CH` usw. landen über die Eltern-Kultur bei `de`.
- Leere Marker-Klasse `Texts` (`Localization/Texts.cs`) mit `Resources/Texts.resx` (Englisch) und
  `Resources/Texts.de.resx`. Schlüssel sind sprechende englische Bezeichner (`Login.Title`, `Register.NameRequired` …),
  nicht der englische Text selbst.
- Culture-Cookie schreiben: Hilfsfunktion `LanguageCookie.Append(HttpResponse, string language)` –
  `CookieRequestCultureProvider.MakeCookieValue`, 1 Jahr, `SameSite=Lax`, `Secure` außer in Development, **nicht**
  HttpOnly (der Client liest es).

### Umschalter (`Pages/Language.cshtml(.cs)`, `Pages/Shared/_LanguageSwitch.cshtml`)
- Partial im `_Layout`: `<form method="post" asp-page="/Language" data-language-switch data-confirm="@T["Language.Confirm"]">`
  mit verstecktem `ReturnUrl` (aktueller Pfad + Query) und zwei Buttons `name="Language" value="de|en"`
  („Deutsch“, „English“ – jeweils in der eigenen Sprache, die aktive markiert und deaktiviert).
- `Language` hat nur `OnPostAsync`:
  1. `GameLanguages.Normalize(Language)`.
  2. Cookie schreiben.
  3. Angemeldet → `Account.Language` setzen, `UpdateAsync` (AK-5).
  4. `LocalRedirect` auf `ReturnUrl`, wenn `Url.IsLocalUrl`, sonst `/` (AK-3).
- Kein Rate-Limit (keine Anmeldedaten), Antiforgery wie jede Razor-Seite.
- Das Layout setzt `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName">`.

### Nachfrage vor dem Umschalten (`client/src/account/language.ts` → `wwwroot/js/language.js`)
- Fester Build wie `passkeys.js`: `vite.passkeys.config.ts` wird zu einer Lib mit zwei Einträgen
  (`passkeys`, `language`), Ausgabe `[name].js`; das Kopieren ins Admin bleibt nur für `passkeys.js`.
- Beim `submit` von `[data-language-switch]`: Hat irgendein sichtbares Eingabefeld der Seite außerhalb des Umschalters
  einen Wert (Text, E-Mail, Passwort), dann `confirm(form.dataset.confirm)`; bei Abbruch `preventDefault()` (AK-3).
- Im `_Layout` immer eingebunden (`<script type="module" src="~/js/language.js" asp-append-version="true">`). Ohne
  JavaScript schaltet der Umschalter ohne Nachfrage um.

### Seiten-Texte
- `_Layout`, `Index`, `Account/Register`, `Login`, `ConfirmEmail`, `ResendConfirmation`, `Logout`: alle sichtbaren
  Texte, `ViewData["Title"]`, Fehlermeldungen und Validierungsmeldungen über `IStringLocalizer<Texts>`
  (`@inject IStringLocalizer<Texts> T` in `_ViewImports`, im Page-Model per Konstruktor). Die deutschen Texte aus T7
  wandern unverändert nach `Texts.de.resx`.
- `Login.GenericError` wird vom `const` zur Ressource.
- `PlayerNames.Describe(error)` liest aus `Texts` über einen statischen `ResourceManager` (aktuelle UI-Kultur); die
  Regeln selbst bleiben unverändert (AK-11).

### Identity-Fehler (`BrowserGame.Data`)
- `GermanIdentityErrorDescriber` → `LocalizedIdentityErrorDescriber` mit `Resources/IdentityErrors.resx` (Englisch)
  und `IdentityErrors.de.resx` (die heutigen deutschen Texte). Liest über einen statischen `ResourceManager` mit der
  aktuellen UI-Kultur; `DuplicateEmail` bleibt in beiden Sprachen neutral.
- Server und Admin registrieren den neuen Describer.

### Admin bis 005
- `AddLocalization` + `UseRequestLocalization` mit genau einer unterstützten Sprache `de` und ohne Provider. So bleibt
  das Admin vollständig deutsch, auch bei englischem Browser. 005 stellt auf `en` um und übersetzt (005 AK-14).

### Account-Sprache
- **Registrierung** (AK-7): `Account.Language = GameLanguages.Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)`.
- **Anmeldung** (AK-6): Nach erfolgreichem `PasswordSignInAsync` Culture-Cookie aus `Account.Language` setzen, vor dem
  Redirect. (Passkey-Anmeldung in 003 T9 macht dasselbe.)
- Abmelden lässt das Culture-Cookie stehen: Das Gerät behält die zuletzt genutzte Sprache.

### Mails (`Accounts/EmailSenders.cs`)
- `GameEmailSender` baut Betreff und Text aus `Texts` (`Mail.*`-Schlüssel) mit `CultureInfo.GetCultureInfo(user.Language)`
  über den statischen `ResourceManager` – unabhängig von der Kultur der Anfrage (AK-9). Platzhalter (`{0}` = Name,
  `{1}` = Link …) per `string.Format(culture, …)`.
- `MailHtml` bleibt sprachneutral; `lang` im HTML der Mail aus `user.Language`.
- Passwort-Reset-Mail (003 T8) nutzt denselben Weg.

## Client

### Wörterbuch (`client/src/i18n.ts`)
- `const en = { … } as const;` gibt die Schlüssel vor, `const de: Texts = { … }` mit `type Texts = Record<keyof typeof en, string>`
  – fehlt ein Schlüssel, bricht `tsc` (AK-10).
- `detectLanguage(cookie: string, browserLanguage: string): "de" | "en"` – reine Funktion: `uic=` aus
  `.AspNetCore.Culture`, sonst `browserLanguage.startsWith("de")`, sonst `en`.
- `export const t = …` einmal beim Laden bestimmt; `main.tsx` setzt `document.documentElement.lang`.
- Kein Umschalter im Spiel (AK-8b).

### Texte
- `App.tsx`: Statusanzeigen („verbinde …“, „verbunden“, „getrennt“), „Neu verbinden“ aus `t`.
- `game.ts`: Fehlertext „Canvas 2D wird nicht unterstützt“ aus `t`.
- `passkeys.ts`: Texte aus einem eigenen kleinen Wörterbuch nach `document.documentElement.lang` (Admin: `de` bis 005).
- Schlüssel für das `kicked`-Overlay aus 003 T13 („Auf einem anderen Gerät angemeldet“, „Hier weiterspielen“) legt
  006 schon an; 003 T13 nutzt sie.

## Tests

- **Test-Clients:** `GameServerFactory` setzt für alle Clients `Accept-Language: de` (`ConfigureClient`), damit die
  bestehenden Integrationstests mit deutschen Texten unverändert bleiben. Tests für Englisch setzen den Header selbst.
- **Bestehende Unit-Tests mit deutschen Texten** (`PlayerNamesTests`, `EmailTests`): Sie hängen danach von der Kultur
  ab. `PlayerNamesTests` setzt die UI-Kultur auf `de` (Verhalten unverändert); `EmailTests` bleibt unverändert, weil
  `Account.Language` standardmäßig `"de"` ist.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `Accept-Language`-Provider, `de*` → Deutsch | Integration: `Accept-Language: de-AT` → deutsche Startseite, `lang="de"` |
| 2 | nur erster Eintrag, Standard `en` | Integration: `fr`, `en-US`, `fr, de` und ohne Header → englisch |
| 3 | `/Language` + Partial im Layout, `language.js` fragt nach | Integration: POST → 302 auf ReturnUrl, Cookie gesetzt, Seite danach englisch; fremde ReturnUrl → `/`. Vitest: Nachfrage nur bei Eingaben. **Manuell:** Abbrechen bleibt auf der Seite |
| 4 | Culture-Cookie 1 Jahr | Integration: `Set-Cookie` mit Expires ≈ 1 Jahr, Folgeanfrage mit Cookie bleibt englisch trotz `Accept-Language: de` |
| 5 | `/Language` aktualisiert `Account.Language` | Integration: angemeldet umschalten → Account in der DB hat `en` |
| 6 | Login setzt Cookie aus Account | Integration: Account `en`, neuer Client mit `Accept-Language: de` meldet sich an → Cookie `en`, Folgeseite englisch |
| 7 | Register speichert aktuelle Sprache | Integration: Registrierung mit `Accept-Language: en` → `Account.Language == "en"` |
| 8 | Client-Wörterbuch | Vitest: `detectLanguage`; **manuell:** HUD in beiden Sprachen |
| 8b | kein Umschalter im Client | Review; **manuell** |
| 9 | Mails nach `Account.Language` | Integration: englischer Account, Bestätigung erneut senden mit `Accept-Language: de` → englische Mail; Unit (`EmailTests`-Stil) je Sprache |
| 10 | alle Texte aus Ressourcen | Unit: `Texts.resx`/`Texts.de.resx` und `IdentityErrors(.de).resx` haben dieselben Schlüssel, keiner leer; `tsc` für den Client; Integration: Registrierungsfehler auf Englisch (Name, Passwort, Pflichtfeld) |
| 11 | Regeln unverändert | bestehende `PlayerNamesTests` grün; Integration: Name „tObIaS…“ auf Englisch registriert → „Tobias…“ |

Admin: bestehende Admin-Tests bleiben grün (fest Deutsch); zusätzlich Integration: `Accept-Language: en` → Admin weiter deutsch.

## Constitution-Check

- [x] **Server ist Autorität:** Die Sprache beeinflusst keinen Spielstand; Account-Sprache speichert nur der Server.
- [x] **Typen nur aus `contracts/`:** keine Vertragsänderung.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine neuen Abhängigkeiten:** ASP.NET Core Localization ist Teil des Frameworks; Client ohne Bibliothek.
- [x] **KISS:** keine Sprache in der URL, kein Claim, kein Nachladen im Client.

## Risiken / Abweichungen

- **`Accept-Language` nur mit erstem Eintrag:** Wer „fr, de“ meldet, bekommt Englisch. So steht es in AK-2.
- **Texte vergessen:** Neue Seiten in 003 (T8, T9, T13) müssen ihre Texte gleich in beide `.resx` bzw. ins
  Client-Wörterbuch schreiben. Der Schlüssel-Abgleich-Test fängt fehlende Übersetzungen, nicht aber Texte direkt im
  Markup – das prüft das Review (ADR 0008).
- **Culture-Cookie ist nicht HttpOnly:** Es enthält nur die Sprache.
