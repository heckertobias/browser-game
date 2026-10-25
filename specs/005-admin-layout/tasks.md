# 005 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Build → Layout → Seiten → Tests → Betrieb.

## Build
- [ ] **T1** (AK-5): `bootstrap` und `bootstrap-icons` per npm (gepinnt); `build:admin` kopiert sie nach
  `Admin/wwwroot/lib/` (gitignored); `client/src/admin.css` und das Kopieren der Admin-Pages im Dockerfile entfernen.

## Layout
- [ ] **T2** (AK-1, AK-2, AK-3, AK-5): `_Layout` mit Seitenleiste (Offcanvas unter `lg`), aktiver Eintrag mit
  `aria-current`, Konto und Abmelden in der Leiste; `wwwroot/css/admin.css`.
- [ ] **T3** (AK-4): `_LayoutPlain` für Login, LoginTwoFactor und Invite.
- [ ] **T4** (AK-10, AK-11, AK-12): `theme.js` synchron im `<head>`, Umschalter Hell/Dunkel/System.
- [ ] **T5** (AK-7, AK-8): `_Errors` (nur Fehler ohne Feld), `_Message`, `BootstrapValidationTagHelper` (`is-invalid`).

## Seiten
- [ ] **T6** (AK-6, AK-7, AK-9, AK-13): Index, Smtp, Admins, Audit, Account auf Bootstrap umstellen.
- [ ] **T7** (AK-6, AK-13): Login, LoginTwoFactor, Invite auf Bootstrap umstellen.

## Tests
- [ ] **T8**: Integrationstests laut Abdeckungstabelle; bestehende Admin-Tests grün (gegen SQLite und PostgreSQL).

## Betrieb
- [ ] **T9**: Release; manuelle Prüfung auf dem Pi (AK-3, AK-6, AK-9–12,
  Handy). Danach alle AKs abhaken und Spec-Status auf `umgesetzt`.
