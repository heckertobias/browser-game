# 017 – Spielfigur als Pixel-Art

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md) (Figuren, Bewegung, Spielerfarbe) ·
[003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (wartende Figuren, AK-20) ·
[015 – Slash-Commands](../015-slash-commands/spec.md) (`/run …` für die Pfeil-Anzeige) ·
[016 – Blickrichtung](../016-blickrichtung/spec.md) (Blickrichtung jeder Figur)
Berührt: [016 – Blickrichtung](../016-blickrichtung/spec.md) – der Pfeil wird zur Entwickleroption

## Ziel

Spieler sind keine farbigen Quadrate mehr, sondern **Pixel-Art-Figuren**. Sie schauen in eine von
**vier Richtungen**, laufen sichtbar und stehen still, wenn sie stehen. Die bisherige **Spielerfarbe**
bleibt an der Figur erkennbar (z. B. an der Kleidung), damit man Spieler weiter unterscheiden kann.

Grundlage ist das Asset-Pack **Ninja Adventure** (CC0). Eigene oder abgeleitete Grafiken folgen seinem
Raster und seiner Palette. Der Pfeil aus 016 verschwindet aus dem normalen Spielbild und lässt sich als
Entwickleroption per Command einschalten.

Dies ist die erste von vier Grafik-Specs: danach [018 – Boden](../018-boden/spec.md),
[019 – Deko-Objekte](../019-deko/spec.md) und [020 – Pixel-Schrift](../020-pixel-schrift/spec.md).

## User Stories

- Als **Spieler** möchte ich eine echte Figur statt eines Quadrats sehen, damit sich das Spiel wie ein
  Spiel anfühlt.
- Als **Spieler** möchte ich sehen, wohin eine Figur schaut und ob sie läuft, damit ich die Welt auf einen
  Blick lese.
- Als **Spieler** möchte ich andere Spieler weiter an ihrer Farbe unterscheiden, bis es eine
  Charakterauswahl gibt.
- Als **Entwickler** möchte ich den genauen Blickwinkel aus 016 bei Bedarf einblenden, damit ich
  Blickrichtung und Gegner-Spawn (014) prüfen kann.
- Als **Betreiber** möchte ich, dass jede Grafik eine Lizenz ohne Auflagen hat und ihre Herkunft
  dokumentiert ist, damit das Spiel rechtlich sauber bleibt.

## Akzeptanzkriterien

### Aussehen

- **AK-1:** Gegeben eine Figur ist sichtbar (eigene oder fremde), dann wird sie als Pixel-Art-Figur
  dargestellt, nicht mehr als Quadrat. Sie nimmt in der Welt etwa die bisherige Fläche ein; Name über der
  Figur, Bewegung und Tempo bleiben unverändert.
- **AK-2:** Gegeben zwei Spieler mit unterschiedlicher Spielerfarbe (001), dann ist an beiden Figuren ihre
  jeweilige Farbe gut erkennbar (z. B. an der Kleidung); der Rest der Figur sieht gleich aus.
- **AK-3:** Gegeben ein beliebiger Bildschirm (007), dann ist die Figur scharf: Alle ihre Pixel sind gleich
  groß, nichts ist verschwommen.

### Richtung

- **AK-4:** Gegeben die Blickrichtung einer Figur (016), dann zeigt die Figur die **nächstliegende** der
  vier Richtungen (oben, unten, links, rechts).
- **AK-5:** Gegeben die Blickrichtung liegt genau zwischen zwei Richtungen (schräg, 45°), dann zeigt die
  Figur die Seitenansicht (links bzw. rechts).
- **AK-6:** Gegeben ich bin neu im Spiel und habe mich noch nicht bewegt, dann zeigt meine Figur die
  Richtung, die sich aus der Standard-Blickrichtung (016 AK-4) ergibt.

### Bewegung

- **AK-7:** Gegeben eine Figur läuft, dann spielt sie eine **Laufanimation** in ihrer Richtung; bei anderen
  Spielern ebenso, sobald sie sich bei mir sichtbar bewegen.
- **AK-8:** Gegeben eine Figur steht, dann zeigt sie ein **Standbild** in ihrer zuletzt gezeigten Richtung;
  sie dreht nicht zurück (016 AK-3).
- **AK-9:** Gegeben eine Figur wartet nach Verbindungsverlust (003 AK-20), dann steht sie still und ist
  weiter sichtbar anders eingefärbt (z. B. ausgegraut).

### Pfeil als Entwickleroption

- **AK-10:** Gegeben ich habe nichts eingestellt, dann sehe ich an keiner Figur den Pfeil aus 016.
- **AK-11:** Gegeben ich gebe `/run toggleFacingArrows()` ein (015), dann sehe ich den Pfeil aus 016 an
  **allen** Figuren in meinem Bild; beim nächsten Mal blende ich ihn wieder aus.
- **AK-12:** Gegeben ich habe die Pfeile eingeschaltet, dann ändert das nur **mein** Bild; andere Spieler
  sehen weiter keine Pfeile.
- **AK-13:** Gegeben ich habe die Pfeile eingeschaltet, wenn ich das Spiel neu lade oder mich neu anmelde,
  dann sind sie wieder aus; die Einstellung wird nicht gespeichert.

### Robustheit

- **AK-14:** Gegeben die Grafik der Figur lässt sich nicht laden, dann sind alle Figuren trotzdem sichtbar
  (in der bisherigen Darstellung als farbiges Quadrat) und das Spiel bleibt spielbar.
- **AK-15:** Gegeben etwa 50 laufende Figuren sind gleichzeitig sichtbar (Vision), dann läuft das Bild auf
  dem Testhandy flüssig, ohne sichtbares Ruckeln.

### Herkunft & Lizenz

- **AK-16:** Gegeben eine Grafik im Spiel, dann sind ihre Herkunft und ihre Lizenz im Repo dokumentiert;
  es werden nur Grafiken mit einer Lizenz ohne Auflagen verwendet (CC0) oder selbst erstellte.
- **AK-17:** Gegeben eine abgeleitete oder eigene Figur (z. B. mit einfärbbarer Kleidung), dann folgt sie
  Raster und Palette von Ninja Adventure.

## Out of Scope

- **Charakterauswahl** (mehrere Figuren zur Wahl) – eigene spätere Spec.
- **Personalisierbare Figuren** (Haare, Rüstung, Waffen) – eigene spätere Spec.
- **Acht Richtungen** oder stufenlos gedrehte Figuren.
- Weitere Animationen: Angriff, Treffer, Tod, Zaubern, Hinsetzen für wartende Figuren.
- Grafik für Gegner und Trainings-Dummy (013/014).
- Boden, Deko-Objekte und Pixel-Schrift (018–020).
- Eine Umschaltung für den Pfeil im Menü; es gibt nur den Command.
- Credits oder Lizenzhinweise im Spiel selbst (CC0 verlangt keine Namensnennung).

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Asset-Pack:** Ninja Adventure (CC0, 16×16, Figuren mit vier Richtungen, Stand und Lauf). Es deckt auch
  Boden, Deko und Gegner im selben Stil ab und hat genug Figuren für eine spätere Charakterauswahl.
- **Abgeleitete Grafiken sind erlaubt:** CC0 erlaubt Änderungen. Für die Spielerfarbe braucht es eine
  abgeleitete Figur mit einfärbbarer Fläche.
- **Vier Richtungen:** Die Figur zeigt die nächstliegende Richtung; genau schräg gewinnt die Seitenansicht.
  Die Blickrichtung selbst bleibt der freie Winkel aus 016.
- **Pfeil nur für Entwickler:** Der Pfeil aus 016 ist standardmäßig aus und per
  `/run toggleFacingArrows()` zuschaltbar. Er wirkt nur auf das eigene Bild und gilt bis zum Neuladen.
  Wie alle Commands aus 015 ist er für jeden angemeldeten Spieler verfügbar.
- **Spielerfarbe bleibt:** Bis zur Charakterauswahl sehen alle Figuren gleich aus und unterscheiden sich
  nur in der Farbe.
- **Lizenzregel:** Nur CC0 oder selbst erstellt. Der Plan hält die Regel als ADR fest.
- **Alle Zahlen sind Konstanten im Plan** (Animationstempo, Größe der Figur); die Spec nennt keine Zahlen.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
