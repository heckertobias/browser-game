# 006 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Daten → Server → Client → Prüfung. Jeder Task bringt seine Tests mit.

## Server – Daten
- [x] **T1** (AK-5–7): `Account.Language` (Standard `"de"`), Migration `AddAccountLanguage` für SQLite und PostgreSQL,
  Bestand `"de"`. `GameLanguages` (`Supported`, `Default`, `Normalize`) + Unit-Test.

## Server – Lokalisierung
- [x] **T2** (AK-1, AK-2, AK-4, AK-10): `AddLocalization`, DataAnnotations-Lokalisierung, `UseRequestLocalization`
  (Cookie → `Accept-Language` mit einem Wert → `en`), `Texts` + `Texts.resx`/`Texts.de.resx`, `LanguageCookie`,
  `<html lang>` im Layout. `GameServerFactory` setzt `Accept-Language: de`. Unit-Test Schlüssel-Abgleich der `.resx`.
- [x] **T3** (AK-10, AK-11): `LocalizedIdentityErrorDescriber` mit `IdentityErrors(.de).resx` in `BrowserGame.Data`
  (ersetzt `GermanIdentityErrorDescriber`, Server und Admin); `PlayerNames.Describe` aus `Texts`; Admin fest `de`
  per `UseRequestLocalization`. Texte je Sprache in neuen Tests.
- [x] **T4** (AK-10): Alle Spieler-Seiten aus 003 T7 (`_Layout`, `Index`, `Register`, `Login`, `ConfirmEmail`,
  `ResendConfirmation`, `Logout`) auf `IStringLocalizer<Texts>`; Integrationstests auf Englisch.
- [x] **T5** (AK-3, AK-5): `Pages/Language` (POST) und `_LanguageSwitch` im Layout; Account-Sprache beim Umschalten.
- [x] **T6** (AK-6, AK-7): Registrierung speichert die aktuelle Sprache, Anmeldung setzt das Culture-Cookie aus dem
  Account.
- [x] **T7** (AK-9): `GameEmailSender` mit Texten aus `Texts` in der Sprache des Accounts; Unit- und Integrationstests
  je Sprache.

## Client
- [x] **T8** (AK-3): `language.ts` als fester Build `wwwroot/js/language.js` (gemeinsame Lib-Konfiguration mit
  `passkeys.js`), im Layout eingebunden; Vitest für die Nachfrage.
- [ ] **T9** (AK-8, AK-8b, AK-10): `i18n.ts` mit `detectLanguage` (+ Vitest), HUD-Texte und Fehlertexte im Client,
  `passkeys.ts` nach `<html lang>`; Schlüssel für das `kicked`-Overlay aus 003 T13.

## Prüfung
- [ ] **T10**: Manuelle Prüfung (lokal und auf dem Pi): Browser auf Deutsch und Englisch, Umschalten mit und ohne
  Eingaben (Nachfrage, Abbrechen), Anmelden auf zweitem Gerät, Mails in beiden Sprachen, HUD in beiden Sprachen.
  In 003 vermerken, dass T8, T9 und T13 ihre Texte zweisprachig anlegen. Danach alle AKs abhaken, Status `umgesetzt`.
