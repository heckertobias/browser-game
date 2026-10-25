# 005 – Plan

Gehört zu: [spec.md](spec.md) · Entscheidung: [ADR 0007](../../docs/decisions/0007-bootstrap-fuers-admin.md)
(Bootstrap statt daisyUI im Admin, ergänzt ADR 0001)

## Überblick

Nur `BrowserGame.Admin` (Razor Pages) und der CSS-Build im `client/` ändern sich. Spiel, Startseite, Datenbank und
Verträge bleiben unberührt.

## Verträge

Keine Änderung.

## Abhängigkeiten & Build

- **npm** (in `client/`, damit Dependabot sie mitpflegt): `bootstrap` 5.3.x und `bootstrap-icons`, exakt gepinnt.
- `build:admin` kompiliert kein Tailwind mehr, sondern kopiert die fertigen Dateien nach
  `server/BrowserGame.Admin/wwwroot/lib/` (gitignored, wie bisher `site.css`):
  - `bootstrap/bootstrap.min.css`, `bootstrap/bootstrap.bundle.min.js` (inkl. Popper, für Offcanvas und Dropdown)
  - `bootstrap-icons/bootstrap-icons.min.css` + `fonts/`
- `client/src/admin.css` entfällt. Das eigene CSS kommt als kleine, handgeschriebene Datei
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
| 13 | alle bestehenden Admin-Tests bleiben grün (ggf. Selektoren anpassen, Abläufe nicht) |

Zusätzlich: Statische Dateien (`/lib/bootstrap/…`, `/js/theme.js`, Icon-Font) liefern 200 im gebauten Image
(Smoke-Test im CI-`docker`-Job bzw. manuell nach dem Release).

## Constitution-Check

- Keine neuen Laufzeitdienste, keine CDN-Abhängigkeit, CSP unverändert.
- Abweichung von ADR 0001 („daisyUI für alle UIs“) → ADR 0007.
