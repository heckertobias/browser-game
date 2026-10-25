# ADR 0008 – Lokalisierung: resx auf dem Server, Wörterbücher im Client

**Status:** akzeptiert

## Kontext

Mit [006](../../specs/006-sprachen/spec.md) gibt es das Spiel auf Deutsch und Englisch. Texte entstehen an drei
Stellen: in den Razor-Seiten und Mails des Servers, in Identity-Fehlern und Namensregeln (geteilt mit dem Admin) und im
Spiel-Client (HUD, Hinweise). Weitere Sprachen und ein lokalisiertes Admin-Interface sollen später ohne Umbau möglich sein.

## Entscheidung

- **Server:** ASP.NET Core Localization (`AddLocalization`, `UseRequestLocalization`, `IStringLocalizer`) mit
  `.resx`-Dateien. Neutrale Datei = Englisch, dazu `*.de.resx`. Eine neue Sprache ist eine weitere `.resx` plus ein
  Eintrag in der Liste der unterstützten Sprachen.
- **Wahl der Sprache pro Anfrage:** Culture-Cookie (`.AspNetCore.Culture`) vor `Accept-Language`, Standard Englisch.
  Die Sprache eines Accounts steht in der Datenbank; beim Anmelden wird das Cookie daraus gesetzt. Keine Sprache in der
  URL.
- **Mails** nutzen die Sprache des Accounts, nicht die der aktuellen Anfrage.
- **Client:** eigene, typisierte Wörterbücher in TypeScript (`en` gibt die Schlüssel vor, `de` muss alle haben). Die
  Sprache kommt aus dem Culture-Cookie, sonst aus dem Browser. Im Spiel ist genau eine Sprache aktiv; Wechsel nur
  außerhalb des Spiels.
- **Keine neue Abhängigkeit:** weder auf dem Server noch im Client.

## Alternativen

- **JSON-Ressourcen statt resx** (z. B. `My.Extensions.Localization.Json`): angenehmer zu lesen, aber eine zusätzliche
  Abhängigkeit für zwei Sprachen.
- **i18next / FormatJS im Client:** Pluralregeln, Interpolation, Nachladen – brauchen wir bei einer Handvoll HUD-Texte
  nicht.
- **Sprache in der URL (`/en/…`):** teilbare Links mit fester Sprache, aber jede Route und jeder Mail-Link müsste sie
  tragen. Die Spec verlangt das nicht.
- **Sprache als Claim im Auth-Cookie:** spart nichts gegenüber dem Culture-Cookie und müsste bei jedem Wechsel neu
  ausgestellt werden.

## Folgen

- Alle Spieler-Texte stehen in `.resx` bzw. im Client-Wörterbuch; Texte direkt im Code oder Markup sind ein
  Review-Finding.
- Das Admin-Interface läuft bis [005](../../specs/005-admin-layout/spec.md) fest auf Deutsch und wechselt dort fest
  auf Englisch – mit demselben Mechanismus, sodass eine zweite Admin-Sprache später nur Übersetzungen braucht.
- Beide Wörterbücher des Clients liegen im Bundle (wenige hundert Byte); aktiv ist immer nur eins.
