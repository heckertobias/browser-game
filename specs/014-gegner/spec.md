# 014 – Gegner

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [012 – HP & Mana](../012-hp-mana/spec.md) (Spieler-HP, Schaden, Tod/Respawn) ·
[013 – Rudimentäres Kampfsystem](../013-kampfsystem/spec.md) (Zielauswahl, Nah-/Fernkampf, Cooldown, zielbares Objekt mit HP) ·
[015 – Slash-Commands](../015-slash-commands/spec.md) (`/run`-Dispatcher, in den `spawnEnemy()` eingehängt wird) ·
[016 – Blickrichtung](../016-blickrichtung/spec.md) (der Gegner erscheint vor dem Spieler)
Voraussetzung für: Belohnungen (XP/Loot), weitere Gegnertypen (geplant)

## Ziel

Der Spieler kann echte Gegner bekämpfen, die zurückschlagen. Ein Gegner erscheint auf Befehl
(`/run spawnEnemy()`) etwas vor dem Spieler, steht still, hat HP und greift jeden Spieler in seiner
Nahkampf-Reichweite automatisch mit Cooldown an (der Schaden geht auf die Spieler-HP aus 012). Der
Spieler greift ihn mit dem Kampfsystem aus 013 an; bei 0 HP ist der Gegner besiegt und verschwindet
dauerhaft (einen neuen erzeugt man per Befehl). Belohnungen (XP, Loot) sind nicht Teil dieser Spec.

## User Stories

- Als **Spieler / Tester** möchte ich mit einem Befehl einen Gegner vor mir erzeugen, damit ich das
  Kämpfen gegen etwas Wehrhaftes prüfen kann.
- Als **Spieler** möchte ich den Gegner anvisieren und mit Nah- oder Fernkampf besiegen, damit ich
  sehe, dass Kämpfen gegen Gegner funktioniert.
- Als **Spieler** möchte ich, dass der Gegner mich in seiner Nähe angreift und mir Schaden zufügt,
  damit der Kampf gefährlich wirkt.
- Als **Spieler** möchte ich, dass ein besiegter Gegner verschwindet, damit ich den Kampf sauber
  abschließen kann.

## Akzeptanzkriterien

### Erzeugen (Spawn)
- **AK-1:** Gegeben ich bin im Spiel, wenn ich `/run spawnEnemy()` eingebe, dann erscheint ein Gegner
  etwas vor meiner Figur (in Blickrichtung nach 016, nicht direkt auf ihr), und es erscheint kein
  Chat-Eintrag darüber (Command, vgl. 015 AK-5).
- **AK-2:** Gegeben ich gebe `/run spawnEnemy()` mehrfach ein, dann entsteht bei jedem Aufruf genau
  ein weiterer Gegner; es gibt im ersten Lauf keine Obergrenze.
- **AK-3:** Gegeben ein Gegner ist da, dann ist er ein zielbares Objekt (013): Ich kann ihn
  antippen/anklicken, um ihn als Ziel zu wählen.

### Bewegung
- **AK-4:** Der Gegner steht still an der Stelle, an der er erzeugt wurde, und bewegt sich nicht.

### Angriff des Gegners
- **AK-5:** Gegeben ein Spieler ist in der festen Nahkampf-Reichweite des Gegners (Konstante im
  Plan), dann greift der Gegner diesen Spieler automatisch (ohne Zutun des Spielers) an: Der
  Spieler erleidet den festen Gegner-Schaden (Konstante im Plan), und der Gegner wartet zwischen
  zwei Treffern eine feste Abklingzeit (Konstante im Plan) ab.
- **AK-6:** Gegeben mehrere Spieler sind in Reichweite, dann greift der Gegner den nächsten an.
- **AK-7:** Gegeben kein Spieler ist in Reichweite, dann greift der Gegner niemanden an (er bleibt
  stehen).
- **AK-8:** Gegeben der Gegner fügt einem Spieler Schaden zu, dann sinkt dessen HP (012); erreicht
  die HP 0, greift 012 (Respawn am Startpunkt). Der Gegner hat dabei kein Sonderverhalten (er
  bleibt stehen und greift weiter jeden in Reichweite an).

### Besiegen
- **AK-9:** Gegeben ich habe den Gegner als Ziel, dann kann ich ihn mit Nahkampf oder Fernkampf
  angreifen (013); jeder Treffer senkt seine HP.
- **AK-10:** Gegeben der Gegner hat 0 HP, dann ist er besiegt und verschwindet sofort dauerhaft
  (kein Respawn); einen neuen Gegner erzeuge ich mit `/run spawnEnemy()`.

### Sichtbarkeit
- **AK-11:** Gegeben ein Gegner ist da, dann ist er als Figur sichtbar und von Spielern und dem
  Trainings-Dummy (013) unterscheidbar; sein aktueller HP-Wert ist erkennbar (z. B. ein Balken über
  ihm), analog 013 AK-18.

### Zugriff
- **AK-12:** Gegeben ich bin im Spiel angemeldet, dann darf ich `spawnEnemy()` nach demselben
  Zugriffsmodell wie 015 ausführen (keine Berechtigungsprüfung, überall aktiv) – bewusste
  Einschränkung im ersten Lauf.

## Out of Scope

- Belohnungen bei besiegtem Gegner (XP, Loot, Drops) – eigene Spec.
- Weitere Gegnertypen (Fernkämpfer, verschiedene Stärken) – spätere Specs.
- Bewegung der Gegner (Wandern, Verfolgen, Patrouillen) – die Gegner stehen im ersten Lauf still.
- Selbstständiges / automatisches Spawnen (Spawn-Gebiete, Höchstzahlen, Nachrücken) – Erzeugen nur
  per `/run spawnEnemy()`.
- Respawn eines Gegners nach dem Tod – er bleibt dauerhaft weg (neu erzeugen per Befehl).
- Gegner, die andere Spieler/NPCs/Gegner angreifen oder sich untereinander bekämpfen.
- Aggro-/Sichtsystem (Wahrnehmung, Verfolgen) – der Gegner greift nur an, was in Reichweite ist.
- Sonderangriffe des Gegners (Fläche, Fernkampf, Statuseffekte).
- Persistenz der Gegner zwischen Sessions – sie existieren nur im laufenden Spiel und nur, wenn
  erzeugt.
- Pfadfindung / Kollision mit Hindernissen (kein Terrain).
- Balancing-Zahlen (HP, Schaden, Cooldowns, Spawn-Abstand) – Konstanten im Plan.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Erzeugen nur per Command:** Gegner entstehen ausschließlich durch `/run spawnEnemy()` – ein
  zusätzlicher Command im Dispatcher aus 015, den 014 einhängt. Jeder Aufruf spawnt genau einen
  Gegner (keine Obergrenze), etwas vor dem Spieler in dessen Blickrichtung (016).
- **Stillstehend:** Der Gegner bewegt sich nicht (keine Verfolgung, kein Wandern).
- **Nahkampf mit Cooldown:** Der Gegner greift automatisch jeden Spieler in seiner festen
  Nahkampf-Reichweite an (bei mehreren: den nächsten), mit festem Schaden je Treffer und fester
  Abklingzeit zwischen Treffern (beides Konstanten im Plan). Außerhalb der Reichweite greift er
  nicht an.
- **Spieler-Tod:** Der Schaden geht auf die Spieler-HP (012); bei 0 HP greift 012 (Respawn am
  Startpunkt). Der Gegner hat dabei kein Sonderverhalten.
- **Besiegen = dauerhaft weg:** Bei 0 HP verschwindet der Gegner sofort und endgültig (kein
  Respawn). Einen neuen erzeugt man nur per `/run spawnEnemy()`.
- **Wiederverwendung aus 013:** Der Gegner ist ein „zielbares Objekt" mit HP; Zielauswahl,
  Nah-/Fernkampf und Cooldown des Spielers kommen unverändert aus 013.
- **Server ist Autorität** (Constitution 2): Positionen, Reichweiten, Schaden, HP und Cooldowns
  rechnet der Server; der Client löst nur `spawnEnemy()` aus bzw. wählt/attackiert (013) und zeigt
  den Effekt.
- **Zugriff wie 015:** `spawnEnemy()` folgt dem 015-Modell (keine Berechtigung, überall aktiv) –
  bewusste Einschränkung.
- **Keine Belohnung, keine Persistenz:** XP/Loot sind out of scope; Gegner existieren nur im
  laufenden Spiel.
- **Alle Zahlen sind Konstanten im Plan** (Gegner-HP, Gegner-Schaden, Gegner-Nahkampf-Reichweite,
  Gegner-Cooldown, Spawn-Abstand vor dem Spieler); die Spec nennt keine Zahlen.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
