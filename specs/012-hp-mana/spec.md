# 012 – HP & Mana

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md)
Voraussetzung für: [013 – Rudimentäres Kampfsystem](../013-kampfsystem/spec.md) (geplant), [014 – Gegner](../014-gegner/spec.md) (geplant)

## Ziel

Der Spieler hat einen Lebenswert (HP) und einen Manawert (Mana), die im HUD sichtbar sind.
Mana ist eine nachwachsende Ressource (Basis für den Fernkampf in 013); HP regeneriert von
selbst nicht. Sinkt HP auf 0, stirbt der Spieler und erscheint wieder am Startpunkt der Welt
mit vollem HP und vollen Mana. Damit HP/Mana auch ohne Gegner (die in 014 kommen) testbar
bleiben, lassen sich HP und Mana direkt vom Server setzen.

## User Stories

- Als **Spieler** möchte ich im HUD sehen, wie viel Leben (HP) und Mana ich gerade habe, damit
  ich mein Vorgehen anpassen kann.
- Als **Spieler** möchte ich, dass sich mein Mana über die Zeit von selbst wieder regeneriert,
  damit ich wieder einen Fernangriff ausführen kann.
- Als **Spieler** möchte ich, dass ich nach dem Tod (HP = 0) wieder am Startpunkt der Welt
  auftauche, damit ich weiterspielen kann.
- Als **Betreiber / Tester** möchte ich HP und Mana eines Spielers direkt vom Server aus setzen
  können, damit sich das Verhalten auch ohne Gegner prüfen und testen lässt.

## Akzeptanzkriterien

### Werte
- **AK-1:** Gegeben der Spieler ist im Spiel, dann hat er einen aktuellen HP-Wert und einen
  aktuellen Mana-Wert, jeweils zwischen 0 und ihrem Maximum (Max-HP, Max-Mana; beide Werte sind
  Konstanten, festgelegt im Plan).
- **AK-2:** Gegeben der Spieler startet das Spiel (Anmeldung oder Frischstart), dann ist sein
  HP voll (Max-HP) und sein Mana voll (Max-Mana).
- **AK-3:** Gegeben der Spieler ist im Spiel und sein Mana ist unter dem Maximum, wenn Zeit
  vergeht, dann steigt sein Mana über die Zeit (Regenerationsrate, Konstante im Plan) bis
  zum Maximum; überschreitet er das Maximum, bleibt es bei Max-Mana.
- **AK-4:** Gegeben der Spieler ist im Spiel und sein HP ist unter dem Maximum, wenn Zeit
  vergeht, dann bleibt sein HP über die Zeit gleich (keine automatische HP-Regeneration).

### Anzeige
- **AK-5:** Gegeben der Spieler ist im Spiel, dann sieht er im HUD seinen aktuellen HP-Wert
  und seinen aktuellen Mana-Wert.
- **AK-6:** Gegeben dem Spieler wird Schaden zugefügt oder sein Mana regeneriert, wenn sich
  der Wert ändert, dann zeigt der HUD den neuen Wert in Echtzeit.

### Schaden und Tod
- **AK-7:** Gegeben dem Spieler wird Schaden zugefügt (Quelle: künftige Gegner, 014), dann
  sinkt sein HP-Wert um die Schadensmenge; sinkt HP auf 0 oder darunter, gilt der Spieler als
  „tot".
- **AK-8:** Gegeben der Spieler ist tot (HP ≤ 0), wenn er wieder „aufwacht" (Respawn), dann
  erscheint er am Startpunkt der Welt (wie in 001) mit vollem HP und vollen Mana.
- **AK-9:** Gegeben der Spieler ist tot, dann erfolgt im ersten Durchstich ein sofortiger
  Respawn – ohne Zwischenzustand, ohne Verzögerung und ohne Todesanimation.

### Testbarkeit
- **AK-10:** Gegeben der Server kann HP und Mana eines Spielers direkt setzen (Ohne Gegner),
  dann lassen sich AK-3, AK-7 und AK-8 direkt im Server-Test prüfen, ohne dass ein Gegner
  vorhanden sein muss. (Debug-Befehle wie `/debug full_health`, mit denen ein Spieler HP selbst
  wieder voll macht, kommen in einer eigenen Chat-Commands-Spec; AK-10 verlangt nur, dass die
  Werte serverseitig setzbar sind.)

## Out of Scope

- Gegner, die dem Spieler Schaden zufügen (→ [014 – Gegner](../014-gegner/spec.md))
- Angriffsmöglichkeiten des Spielers (Fern- und Nahkampf) (→ [013 – Rudimentäres Kampfsystem](../013-kampfsystem/spec.md))
- Debug-Befehle wie `/debug full_health` (→ eigene Chat-Commands-Spec)
- Persistenz von HP/Mana zwischen Sessions (beide Werte setzen sich bei jeder Anmeldung wieder
  auf das Maximum zurück)
- Todesanimation, Respawn-Zeitraffer, Auferstehungs- / „Safe-“Zonen
- Ausrüstung, Tränke oder andere HP/Mana-Quellen
- Rassen / Klassen mit unterschiedlichen Startwerten
- Schildwerte, Bonus-HP oder mehrere HP-Werte

## Offene Fragen

_Keine._

## Entschiedene Fragen

- HP/Mana sind **pro Session**: bei jeder Anmeldung (Frisch- oder Wiederaufnahme an der
  gespeicherten Position) setzen sie sich auf das Maximum zurück. [003] speichert die Position,
  aber nicht HP/Mana.
- Mana regeneriert über die Zeit; HP regeneriert **nicht** von selbst.
- Die Regenerationsrate und die Max-Werte (Max-HP, Max-Mana) sind **konfigurierbare
  Konstanten**, im Plan festgelegt; die Spec nennt keine festen Zahlen.
- Der Respawn erfolgt am **Startpunkt der Welt** (wie in 001), nicht an der zuletzt
  gespeicherten Position (003).
- Bei HP ≤ 0 erfolgt **sofortiger Respawn**: keine Zwischenphase, keine Animation, keine
  Verzögerung.
- HP/Mana werden **im HUD** angezeigt (001 bringt das HUD; 012 erweitert es um HP/Mana).
- Für die Testbarkeit (AK-10) muss der Server in der Lage sein, HP/Mana direkt zu setzen; ein
  Spieler-Debug-Befehl (`/debug full_health`) kommt in der eigenen Chat-Commands-Spec.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
