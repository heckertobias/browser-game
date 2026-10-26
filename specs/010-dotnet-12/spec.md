# 010 – Wechsel auf .NET 12

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [009 – Wechsel auf .NET 11](../009-dotnet-11/spec.md) · Veröffentlichung von .NET 12 (voraussichtlich
November 2027) plus 7 Tage Wartezeit (ADR 0002)

## Ziel

Spiel und Admin laufen auf .NET 12, der nächsten Version mit Langzeit-Support. Damit ist der kürzere Support von
.NET 11 rechtzeitig abgelöst.

## User Stories

- Als **Betreiber** möchte ich auf einer .NET-Version mit Langzeit-Support laufen, damit Sicherheitsupdates lange
  gesichert sind.
- Als **Betreiber** möchte ich nach dem Wechsel ein Spiel, das sich für Spieler genauso verhält wie vorher.

## Akzeptanzkriterien

- **AK-1:** Gegeben der Wechsel ist ausgerollt, dann funktionieren alle bisherigen Abläufe von Spiel und Admin
  unverändert (alle bestehenden automatischen Tests grün, Sichtprüfung im Release Candidate in der Testumgebung).
- **AK-2:** Gegeben die Regeln aus 009 (Mindestalter von Paketen, gehärtete Images), dann gelten sie unverändert weiter.
- **AK-3:** Gegeben .NET 11 ist nicht mehr unterstützt, dann läuft zu diesem Zeitpunkt nichts mehr darauf.

## Out of Scope

- Neue Features, die .NET 12 möglich macht – eigene Specs, wenn wir sie brauchen

## Offene Fragen

- [ ] Zeitplan: Wann endet der Support von .NET 11 genau, und wie viel Puffer planen wir vor dem Ende ein?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
