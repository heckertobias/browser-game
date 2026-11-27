# 022 – Startbereich als Spielwert

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (Startpunkt beim ersten Spiel) ·
[012 – HP & Mana](../012-hp-mana/spec.md) (Respawn am Startpunkt)
Berührt: [013 – Kampfsystem](../013-kampfsystem/spec.md) (Trainings-Dummy außerhalb des Startbereichs) ·
[015 – Slash-Commands](../015-slash-commands/spec.md) (`resetPosition()`) · [018 – Boden](../018-boden/spec.md)
(Lage des Startpunkts auf der Karte) · [021 – Kollision](../021-kollision/spec.md) (ungültige Position → Startpunkt)

## Ziel

Wo neue und wiederbelebte Figuren erscheinen, ist ein benannter Spielwert an der zentralen Stelle, wie alle anderen
Spielwerte ([Constitution §8](../constitution.md), [ADR 0015](../../docs/decisions/0015-spielwerte-einstellbar.md)).
Heute stehen Mitte und Größe des Startbereichs fest in der Spiellogik. Danach lässt sich der Startbereich verschieben,
etwa auf den Startpunkt der Karte aus 018, ohne die Spiellogik umzubauen.

## User Stories

- Als **Spieldesigner** möchte ich Lage und Größe des Startbereichs an einer Stelle ändern, damit ich ihn an die Karte
  anpassen kann.
- Als **Entwickler** möchte ich in Tests einen eigenen Startbereich vorgeben, damit sich erster Start und Respawn genau
  prüfen lassen (Constitution §7, §8).
- Als **Spieler** merke ich davon nichts, solange niemand die Werte ändert.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Standardwerte, dann verhält sich das Spiel für Spieler unverändert: Neue Figuren (003 AK-17)
  und wiederbelebte Figuren (012 AK-8) erscheinen zufällig im heutigen Startbereich.
- **AK-2:** Gegeben jemand ändert Mitte oder Größe des Startbereichs an der zentralen Stelle der Spielwerte, wenn der
  Server danach startet, dann erscheinen neue und wiederbelebte Figuren nur noch im neuen Bereich, ohne weitere
  Änderung an der Spiellogik.
- **AK-3:** Gegeben die Größe des Startbereichs ist null, dann erscheinen alle Figuren genau an seiner Mitte.
- **AK-4:** Gegeben eine gespeicherte Position (003 AK-18), dann startet die Figur dort, egal wie der Startbereich
  eingestellt ist.
- **AK-5:** Gegeben ein automatischer Test gibt einen eigenen Startbereich vor, dann gilt genau dieser für ersten Start
  und Respawn, und der Test braucht dafür weder Netz noch Datenbank.

## Out of Scope

- Wo und wie Spielwerte konfiguriert werden (Datei, Admin-Interface): eigene Spec (Constitution §8).
- Mehrere Startbereiche, ein Startbereich je Gebiet, die Markierung des Startpunkts (018 AK-7).
- Prüfen, ob der Startbereich auf begehbarem Boden liegt (021) oder den Trainings-Dummy einschließt (013 AK-13):
  Wer die Werte ändert, stimmt sie ab.
- Werte, die nur Netzwerk-Schwankungen ausgleichen oder Fluten begrenzen, etwa wie lange eine Figur ohne neue Eingabe
  weiterläuft oder wie viele Eingaben warten dürfen. Das sind technische Grenzen im Sinne von Constitution §8, keine
  Spielwerte; sie bleiben benannte Konstanten.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Ein Startbereich für alles:** Alles, was den „Startpunkt der Welt“ nutzt (003 AK-17, 012 AK-8, später 015 AK-7
  und 021 AK-4), meint den Startbereich aus dieser Spec.
- **Form:** Der Startbereich bleibt ein Kreis um eine Mitte, wie heute; Mitte und Radius sind die beiden Spielwerte.
- **Zeitpunkt:** ADR 0015 erlaubt, bestehende Werte erst beim nächsten Anfassen des Features umzustellen. Diese Spec
  zieht das für den Startbereich vor, weil 018 die Lage des Startpunkts festlegen will.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
