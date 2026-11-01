# 005 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidung: [ADR 0007](../../docs/decisions/0007-bootstrap-fuers-admin.md)
(Bootstrap statt daisyUI im Admin, ergänzt ADR 0001)

## Überblick

Es ändern sich `BrowserGame.Admin` (Razor Pages, Texte, SMTP-Seite), der CSS-Build im `client/` und in
`BrowserGame.Data` die Fehlertexte von `SmtpMailer` (englisch, resx) und `SmtpSettingsStore.SaveAsync` (ohne
Benutzername kein Passwort, AK-18). Spiel, Startseite, Datenbankschema und
Verträge bleiben unberührt.

## Verträge

Keine Änderung.

## Abhängigkeiten & Build

- **npm** (in `client/`, damit Dependabot sie mitpflegt): `bootstrap` 5.3.x und `bootstrap-icons`, exakt gepinnt.
- `build:admin` kompiliert kein Tailwind mehr, sondern kopiert die fertigen Dateien nach
  `server/BrowserGame.Admin/wwwroot/lib/` (gitignored, wie bisher `site.css`):
  - `bootstrap/bootstrap.min.css`, `bootstrap/bootstrap.bundle.min.js` (inkl. Popper, für Offcanvas und Dropdown)
  - `bootstrap-icons/bootstrap-icons.min.css` + `fonts/`
- `client/src/styles/admin.css` entfällt. Das eigene CSS kommt als kleine, handgeschriebene Datei
  `server/BrowserGame.Admin/wwwroot/css/admin.css` (eingecheckt): Breite der Seitenleiste, Layout im Grid.
- Dockerfile: Das Kopieren von `server/BrowserGame.Admin/Pages/` in die Client-Stage entfällt (Tailwind scannt dort nicht
  mehr).
- **CSP bleibt unverändert** (`default-src 'self'`, `img-src 'self' data:`): alles kommt vom eigenen Host, keine
  Inline-Skripte. Bootstraps SVG-Icons in Formularen sind `data:`-Bilder; Bootstrap Icons sind ein Font von `'self'`.

## Layout (`Pages/Shared/`)

- **`_Layout.cshtml`** (angemeldet): `<html data-bs-theme>`, Seitenleiste links (`nav` mit `aria-label`) mit
  Einträgen aus einer Liste im Layout (Übersicht, SMTP, Admins, Protokoll; je Icon + Text). Der aktive Eintrag
  bekommt `active` + `aria-current="page"`, ermittelt über `ViewContext.RouteData` (`page`). Unten in der Leiste:
  Konto (Name), Farbschema-Umschalter, Abmelden (POST-Formular wie bisher). Inhalt rechts: Seitentitel (`h1`), dann
  Meldungen, dann `RenderBody()`; maximale Inhaltsbreite für Formulare, Tabellen nutzen die volle Breite.
- **Handy (< `lg`):** Leiste als Bootstrap-Offcanvas (`offcanvas-lg`): oben eine schmale Kopfzeile mit Menü-Knopf,
  ab `lg` steht die Leiste fest links. Keine eigene JS-Logik.
- **`_LayoutPlain.cshtml`** (nicht angemeldet): zentrierte Karte, kein Menü (AK-4). Login, LoginTwoFactor und Invite
  setzen `Layout = "_LayoutPlain"`. Invite im Schritt „Fertig“ verlinkt zur Übersicht wie bisher.
- **Farbschema** (AK-10–12): `wwwroot/js/theme.js` wird **synchron im `<head>`** geladen (vor dem ersten Rendern,
  daher kein Aufblitzen). Es liest `localStorage["admin-theme"]` (`light` | `dark` | `auto`, Standard `auto`) und
  setzt `data-bs-theme`; bei `auto` per `matchMedia('(prefers-color-scheme: dark)')`, inkl. Listener für Wechsel des
  Systems. Der Umschalter ist ein Dropdown mit drei Einträgen (`data-theme-value`), die Klick-Behandlung hängt
  `theme.js` nach `DOMContentLoaded` an. `localStorage` in `try/catch`; ohne Speicher gilt `auto`.
- **Meldungen:** `_Errors` → Bootstrap `alert alert-danger` mit `asp-validation-summary="ModelOnly"` (nur Fehler ohne
  Feld, AK-7); weiter nur sichtbar, wenn ModelState ungültig ist. Neues Partial `_Message` für Erfolg
  (`alert alert-success`) und Hinweise (`alert-warning`) statt der handgeschriebenen `div`s in jeder Seite.

## Formulare (AK-6, AK-7)

- Markup pro Feld: `<label class="form-label">` über `<input class="form-control">` / `<select class="form-select">`,
  Hinweis als `<div class="form-text">`, Fehler als `<span asp-validation-for class="invalid-feedback">`.
- **Feld-Markierung:** Razor setzt bei Fehlern die Klasse `input-validation-error`, Bootstrap erwartet `is-invalid`.
  Ein kleiner TagHelper `BootstrapValidationTagHelper` (für `input`, `select`, `textarea` mit `asp-for`) ergänzt
  `is-invalid`, wenn ModelState für das Feld Fehler hat. Damit `invalid-feedback` sichtbar ist, muss es Geschwister
  des Feldes sein (Bootstrap-Selektor `.is-invalid ~ .invalid-feedback`).
- Fehler werden mit Feldnamen gesetzt, wo es ein Feld gibt (heute schon: `Input.Host`, `Input.Port`, `TestRecipient`);
  Fehler ohne Feld (Mailversand, Passkey, Anmeldung) bleiben oben.
- Buttons: `btn btn-primary` für die Hauptaktion, `btn btn-outline-secondary` für Nebenaktionen (z. B. Testmail),
  `btn btn-outline-danger btn-sm` für „Entfernen“.

## Seiten

Inhaltlich unverändert, nur Markup:
- **Index:** Karten bzw. Liste mit Links zu den Bereichen (wie heute, im neuen Stil).
- **Smtp:** zwei Abschnitte (`card`): „Verbindung“ (Server, Port, Verschlüsselung, Benutzer, Passwort, Absender) und
  „Testen“ (Empfänger + Button); Speichern am Ende. Grid mit `row g-3` / `col-md-6`.
- **Admins, Audit:** `table table-hover align-middle` in `table-responsive` (AK-9); Status als `badge`.
- **Account, Login, LoginTwoFactor, Invite:** Formulare wie oben; QR-Code weiter auf weißem Hintergrund;
  Wiederherstellungscodes als Monospace-Liste.
- `data-testid`-Attribute bleiben erhalten.

## Sprache (AK-14)

Gleicher Mechanismus wie im Spiel ([ADR 0008](../../docs/decisions/0008-lokalisierung.md)), nur mit einer Sprache:

- **Ressourcen:** `server/BrowserGame.Admin/Resources/AdminTexts.cs` (Markertyp, wie `Texts` im Spiel) +
  `AdminTexts.resx` (neutral = Englisch). Keine `.de.resx`. Eine deutsche Fassung ist später `AdminTexts.de.resx` plus
  Eintrag in der Liste der Sprachen – ohne Umbau.
- **Kultur:** `AddLocalization(o => o.ResourcesPath = "")`; `UseRequestLocalization` fest auf `en` (eine Kultur,
  keine Provider – Browser und Culture-Cookie des Spiels ändern nichts). Ersetzt das heutige feste `de`.
- **Views:** `_ViewImports` injiziert `IStringLocalizer<AdminTexts> T`; Texte im Markup nur über `T["…"]`.
  `<html lang>` kommt aus `CultureInfo.CurrentUICulture` – damit wählt `passkeys.js` die englischen Fehlertexte.
- **PageModels:** Meldungen, Fehler (`AddModelError`) und Mails über `IStringLocalizer<AdminTexts>`.
- **Validierung:** `AddDataAnnotationsLocalization` mit `AdminTexts` (wie `PagesSetup` im Spiel); `ErrorMessage` ist ein
  Schlüssel, z. B. `Validation.HostRequired`.
- **Identity-Fehler:** `LocalizedIdentityErrorDescriber` liefert mit `en` schon Englisch (neutrale `IdentityErrors.resx`).
- **Mailserver-Fehler:** Die Texte in `SmtpMailer` (`MailResult.Error`) stehen heute deutsch im Code. Sie wandern nach
  `BrowserGame.Data/Resources/MailErrors.resx` (neutral = Englisch), Zugriff wie `LocalizedIdentityErrorDescriber`
  über `ResourceManager` und `CurrentUICulture`. Das Spiel loggt sie nur; dort ändert sich nur die Sprache im Log.
- **Mails aus dem Admin:** Einladung und Testmail (Betreff, Text, HTML) aus `AdminTexts`.
- **Protokoll:** Neue Einträge (`Action`, `Details`) werden englisch geschrieben. Bestehende Einträge bleiben, wie sie
  gespeichert sind – das Protokoll ist ein Nachweis, keine Übersetzung.
- **Bestehende Tests** prüfen heute deutsche Texte. Sie werden nur in den erwarteten Texten auf Englisch umgestellt,
  Abläufe und Prüfungen bleiben. `AdminLanguageTests` prüft künftig: fest Englisch, auch mit `Accept-Language: de` und
  Culture-Cookie `de`.

## SMTP-Passwort (AK-15–AK-18)

Heute: Die Testmail nutzt die Werte im Formular (004 AK-9). Danach leert die Seite das Passwortfeld; wer dann speichert,
schickt ein leeres Passwort, und der Store behält still das alte (`SaveAsync`: leer = unverändert).

- **Getestetes Passwort mitnehmen (AK-15):** Nach einer **erfolgreichen** Testmail mit eingetipptem Passwort gibt die
  Seite ein verstecktes Feld `TestedPassword` zurück: das Passwort, verschlüsselt mit Data Protection
  (`ToTimeLimitedDataProtector`, eigener Purpose `Admin.Smtp.TestedPassword`, 30 Minuten gültig). Klartext geht nie zurück
  an den Browser (004 AK-8). Unter dem Passwortfeld steht dann: „The tested password will be saved.“
- **Speichern:** Vorrang hat ein eingetipptes Passwort. Sonst gilt ein gültiges `TestedPassword`. Ist das Token
  abgelaufen oder ungültig, speichert die Seite nichts und meldet am Feld `Input.Password`, das Passwort erneut
  einzugeben. Ohne beides bleibt das gespeicherte Passwort (004 AK-8).
- **Status sichtbar (AK-16, AK-18):** Am Passwortfeld steht immer der gespeicherte Stand (`badge`):
  „stored“ / „not stored“, ohne Benutzername „no login“. Ist ein Benutzername gespeichert, aber kein Passwort, zeigt die
  Seite einen Hinweis (`alert-warning`): die Anmeldung am Mailserver wird scheitern. Ohne Benutzername kein Hinweis.
- **Ohne Anmeldung (AK-18):** `SmtpMailer` meldet sich schon heute nur mit Benutzername an. Neu: `SmtpSettingsStore.SaveAsync`
  entfernt das gespeicherte Passwort, wenn der Benutzername leer ist (statt es still zu behalten); das Protokoll nennt
  dann `Password` als geändertes Feld. Das Passwortfeld bleibt bedienbar; ohne Benutzername wird ein eingegebenes
  Passwort nicht gespeichert, die Seite sagt das am Feld (`form-text`).
- **Testmail mit gespeichertem Stand (AK-17):** Zweiter Button im Abschnitt „Test“, Handler `TestSaved`, nur sichtbar,
  wenn Einstellungen gespeichert sind. Er nutzt die Instanzmethode `SmtpMailer.SendAsync(to, …)`, die die Einstellungen
  aus dem Store lädt – denselben Weg wie `PlayerEmailSender` im Spiel. Formularwerte und deren Validierung spielen dafür
  keine Rolle (nur der Empfänger wird geprüft); die Eingaben im Formular bleiben stehen. Protokoll wie bei der Testmail,
  mit dem Zusatz „saved settings“.
- Die Formular-Testmail (004 AK-9) bleibt; ihre Beschriftung sagt klar „with form values“, der zweite Button
  „with saved settings“.

## Tests & AK-Abdeckung

| AK | Prüfung |
|---|---|
| 1 | Integration: angemeldet → `nav` enthält alle Einträge; auf `/Smtp` trägt genau der SMTP-Eintrag `aria-current="page"` |
| 2 | Integration: jede angemeldete Seite enthält Konto-Link und Abmelden-Formular |
| 3 | **manuell** (Handy bzw. schmales Fenster): Leiste eingeklappt, Menü-Knopf öffnet sie, kein seitliches Scrollen |
| 4 | Integration: `/Login` und Einladungsseite enthalten keine Seitenleiste |
| 5 | Integration: Admin-Seiten binden Bootstrap ein, nicht mehr `site.css`; **manuell**: Optik; Spiel-Tests unverändert grün |
| 6 | **manuell** (Sichtprüfung aller Formulare) |
| 7 | Integration: SMTP mit `host:993` → `Input.Host` trägt `is-invalid`, Fehlertext steht am Feld, nicht im Kasten oben; fehlgeschlagene Testmail → Kasten oben; fehlerfreie Seite → kein `alert-danger` |
| 8 | Integration: Speichern → `alert-success` mit „Gespeichert.“ |
| 9 | Integration: Admins/Audit in `table-responsive`; **manuell** auf dem Handy |
| 10–12 | **manuell**: System hell/dunkel, Umschalter, Neuladen, Abmelden, kein Aufblitzen bei „Dunkel“; Integration: `theme.js` wird im `<head>` vor dem CSS geladen und ist ohne Inline-Skript |
| 13 | alle bestehenden Admin-Tests bleiben grün (Selektoren und erwartete Texte anpassen, Abläufe nicht) |
| 14 | Integration: Seiten mit `Accept-Language: de` und Culture-Cookie `de` sind englisch (`lang="en"`, Texte); Einladungs- und Testmail englisch (`FakeSmtpServer`); Validierungs- und Mailserver-Fehler englisch |
| 15 | Integration: Testmail mit neuem Passwort → Speichern mit leerem Feld → Store hat das neue Passwort; abgelaufenes oder manipuliertes Token → nichts gespeichert, Fehler an `Input.Password`; HTML enthält das Passwort nie im Klartext |
| 16 | Integration: Benutzername ohne Passwort gespeichert → „not stored“ + `alert-warning`; mit Passwort → „stored“, kein Hinweis |
| 18 | Unit (`SmtpSettingsStoreTests`): leerer Benutzername entfernt ein gespeichertes Passwort; Integration: ohne Benutzername → „no login“, kein `alert-warning`, Testmail an `FakeSmtpServer` ohne `AUTH` |
| 17 | Integration: gespeicherte Einstellungen, abweichende Formularwerte → `TestSaved` schickt über den gespeicherten Server (`FakeSmtpServer`), nicht über den im Formular; ohne gespeicherte Einstellungen kein Button |

**Texte in Tests:** Tests prüfen keine wörtlichen UI-Texte. Sie prüfen Struktur und Zustand (`data-testid`,
`alert-success`, `is-invalid`, `aria-current`, Store-Inhalt, Mails im `FakeSmtpServer`). Wo ein Text selbst das
Ergebnis ist (AK-14, Mail-Inhalt), kommt der erwartete Wert aus `AdminTexts` bzw. `MailErrors`, nicht als Literal.
Bestehende Tests, die ohnehin wegen AK-14 angepasst werden, wechseln dabei auf diese Art – Abläufe bleiben gleich.

Zusätzlich: Statische Dateien (`/lib/bootstrap/…`, `/js/theme.js`, Icon-Font) liefern 200 im gebauten Image
(Smoke-Test im CI-`docker`-Job bzw. manuell nach dem Release).

## Constitution-Check

- Keine neuen Laufzeitdienste, keine CDN-Abhängigkeit, CSP unverändert.
- Lokalisierung ohne neue Abhängigkeit, nach ADR 0008. Data Protection ist schon im Einsatz (SMTP-Passwort im Store).
- Abweichung von ADR 0001 („daisyUI für alle UIs“) → ADR 0007.
