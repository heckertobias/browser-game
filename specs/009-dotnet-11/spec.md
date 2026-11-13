# 009 – Wechsel auf .NET 11

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

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
  unverändert (alle bestehenden automatischen Tests grün, Sichtprüfung im Release Candidate in der Testumgebung).
- **AK-2:** Gegeben jemand fügt ein NuGet-Paket auf dem vorgesehenen Weg hinzu oder aktualisiert es (das Werkzeug
  wählt die Version), dann wird keine Version genommen, die jünger als 7 Tage ist. Das gilt für Menschen, Agenten
  und Dependabot gleichermaßen.
- **AK-3:** Gegeben eine dringende Sicherheitskorrektur, dann lässt sich die 7-Tage-Regel für genau dieses Paket
  bewusst und nachvollziehbar übergehen.
- **AK-4:** Gegeben die Laufzeit-Images, dann bleiben sie so schlank und gehärtet wie bisher (keine zusätzlichen
  Pakete, Betrieb ohne root, schreibgeschütztes Dateisystem).

## Out of Scope

- Neue Features, die .NET 11 möglich macht – eigene Specs, wenn wir sie brauchen
- Wechsel der Datenbank-Provider oder anderer Bibliotheken über das hinaus, was .NET 11 erfordert

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Mindestalter nur über den vorgesehenen Weg, keine CI-Prüfung:** Laut .NET-11-Doku filtert NuGet zu junge
  Versionen nur, wenn das Werkzeug die Version selbst wählt; eine ausdrücklich angegebene Version und der normale
  Restore gepinnter Versionen prüfen das Alter nicht. Deshalb gilt ab sofort: NuGet-Pakete nur mit dem
  Hinzufügen-/Aktualisieren-Befehl des Werkzeugs ohne feste Version, nie von Hand in die Projektdatei. Eine
  zusätzliche Prüfung in der CI gibt es nicht. Die Ausnahme für Sicherheitsfixes (AK-3) ist die ausdrücklich
  angegebene Version, begründet im PR.

- LTS oder STS: Wir wechseln trotz kürzerem Support auf .NET 11 und danach rechtzeitig auf .NET 12 (LTS,
  [010](../010-dotnet-12/spec.md)).

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
