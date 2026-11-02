# 005 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Build → Sprache → Layout → Seiten → SMTP → Tests → Betrieb.

## Build
- [x] **T1** (AK-5): `bootstrap` und `bootstrap-icons` per npm (gepinnt); `build:admin` kopiert sie nach
  `Admin/wwwroot/lib/` (gitignored); `client/src/styles/admin.css` und das Kopieren der Admin-Pages im Dockerfile entfernen.

## Sprache
- [x] **T2** (AK-14): `AdminTexts(.resx)`, Kultur fest `en`, `IStringLocalizer` in `_ViewImports`,
  DataAnnotations-Lokalisierung; `SmtpMailer`-Fehler nach `MailErrors.resx`; `AdminLanguageTests` auf „fest Englisch“.

## Layout
- [x] **T3** (AK-1, AK-2, AK-3, AK-5, AK-14): `_Layout` mit Seitenleiste (Offcanvas unter `lg`), aktiver Eintrag mit
  `aria-current`, Konto und Abmelden in der Leiste; `<html lang>` aus der Kultur; `wwwroot/css/admin.css`.
- [x] **T4** (AK-4): `_LayoutPlain` für Login, LoginTwoFactor und Invite.
- [x] **T5** (AK-10, AK-11, AK-12): `theme.js` synchron im `<head>`, Umschalter Hell/Dunkel/System.
- [x] **T6** (AK-7, AK-8): `_Errors` (nur Fehler ohne Feld), `_Message`, `BootstrapValidationTagHelper` (`is-invalid`).

## Seiten
- [x] **T7** (AK-6, AK-7, AK-9, AK-13, AK-14): Index, Smtp, Admins, Audit, Account auf Bootstrap und Englisch umstellen
  (inkl. Einladungs- und Testmail, Protokolleinträge).
- [x] **T8** (AK-6, AK-13, AK-14): Login, LoginTwoFactor, Invite auf Bootstrap und Englisch umstellen.

## SMTP
- [x] **T9** (AK-15, AK-16, AK-17): `TestedPassword` (zeitbegrenzt geschützt) nach erfolgreicher Testmail und beim
  Speichern; Passwort-Status und Hinweis bei Benutzername ohne Passwort; Handler `TestSaved` über `SmtpMailer` mit
  gespeichertem Stand.
- [x] **T9b** (AK-18): `SmtpSettingsStore.SaveAsync` entfernt das Passwort ohne Benutzername; Status „no login“, kein
  Hinweis.

## Tests
- [ ] **T10**: Integrationstests laut Abdeckungstabelle; bestehende Admin-Tests grün (gegen SQLite und PostgreSQL).

## Betrieb
- [ ] **T11**: Release; manuelle Prüfung in der Testumgebung (AK-3, AK-6, AK-9–12,
  Handy). Danach alle AKs abhaken und Spec-Status auf `umgesetzt`.
