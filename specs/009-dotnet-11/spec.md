# 009 – Wechsel auf .NET 11

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: Veröffentlichung von .NET 11 (voraussichtlich November 2026) plus 7 Tage Wartezeit (ADR 0002)

## Ziel

Spiel und Admin laufen auf .NET 11. Damit gilt auch für NuGet-Pakete die Regel „nur Versionen, die mindestens 7 Tage
alt sind“ direkt im Werkzeug – so wie bei npm schon heute –, statt nur über Dependabot.

## User Stories

- Als **Betreiber** möchte ich, dass beim Hinzufügen oder Aktualisieren von Paketen keine Versionen genommen werden,
  die jünger als 7 Tage sind, egal ob von Hand, durch Agenten oder durch Dependabot.
- Als **Betreiber** möchte ich nach dem Wechsel ein Spiel, das sich für Spieler genauso verhält wie vorher.

## Akzeptanzkriterien

- **AK-1:** Gegeben der Wechsel ist ausgerollt, dann funktionieren alle bisherigen Abläufe von Spiel und Admin
  unverändert (alle bestehenden automatischen Tests grün, Sichtprüfung im Release Candidate auf dem Pi).
- **AK-2:** Gegeben jemand fügt ein NuGet-Paket hinzu oder aktualisiert es, dann wird keine Version genommen, die
  jünger als 7 Tage ist.
- **AK-3:** Gegeben eine dringende Sicherheitskorrektur, dann lässt sich die 7-Tage-Regel für genau dieses Paket
  bewusst und nachvollziehbar übergehen.
- **AK-4:** Gegeben die Laufzeit-Images, dann bleiben sie so schlank und gehärtet wie bisher (keine zusätzlichen
  Pakete, Betrieb ohne root, schreibgeschütztes Dateisystem).

## Out of Scope

- Neue Features, die .NET 11 möglich macht – eigene Specs, wenn wir sie brauchen
- Wechsel der Datenbank-Provider oder anderer Bibliotheken über das hinaus, was .NET 11 erfordert

## Offene Fragen

- [ ] **LTS oder STS?** .NET 10 ist eine Version mit Langzeit-Support (3 Jahre), .NET 11 eine mit Standard-Support
      (kürzer). Wechseln wir trotzdem sofort, oder bleiben wir bis .NET 12 (LTS) auf .NET 10 und nutzen bis dahin nur
      Dependabot plus die Regel für Agenten? Empfehlung: wechseln – das Spiel ist früh in der Entwicklung, der kürzere
      Support ist bis .NET 12 kein Problem, und die 7-Tage-Regel wirkt dann auch bei NuGet direkt.
- [ ] Wie genau greift das Mindestalter in NuGet (laut Release Notes `minPublishAgeHours` ab .NET 11 RC2, vor allem
      für gleitende Versionen)? Reicht es für AK-2 bei fest gepinnten Versionen, oder braucht es zusätzlich eine
      Prüfung in der CI? Vor dem Plan mit der finalen .NET-11-Doku klären.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
