# Grafiken und Schriften – Herkunft und Lizenz

Jede Grafik und jede Schrift in diesem Ordner steht hier mit genau einer Zeile. Erlaubt sind nur CC0 1.0 und selbst
erstellte Dateien ([ADR 0017](../../../docs/decisions/0017-grafiken-nur-cc0.md)). `licenses.test.ts` prüft das.

- **Datei:** Pfad relativ zu diesem Ordner, in Backticks, z. B. `` `sprites/humanoid/body-default.png` ``.
- **Herkunft:** Name und Urheber der Quelle, Link, Pfad in der Quelle; bei eigenen Dateien „selbst erstellt“.
- **Stand:** Version oder Update-Datum der Quelle.
- **Lizenz:** genau `CC0 1.0` oder `eigen`, ohne Backticks.
- **Änderungen:** was gegenüber der Quelle geändert wurde, sonst „keine“.

| Datei | Herkunft | Stand | Lizenz | Änderungen |
|---|---|---|---|---|
| `sprites/humanoid/body-default.png` | Ninja Adventure – Asset Pack von Pixel-boy und AAA, https://pixel-boy.itch.io/ninja-adventure-asset-pack, aus `Actor/Character/NinjaBlue/SpriteSheet.png` und `Actor/Character/RedNinja3/SpriteSheet.png` | Update #8 (Animation V2), 2026-03-29 | CC0 1.0 | abgeleitet mit `client/scripts/import-sprites.mjs`: Laufbilder (Pack-Zeilen 0–3) von NinjaBlue in Zellen 48×48, Körper ohne die 1512 Kleidungspixel, Augen in der Farbe von NinjaBlue |
| `sprites/humanoid/clothing-ninja.png` | Ninja Adventure – Asset Pack von Pixel-boy und AAA, https://pixel-boy.itch.io/ninja-adventure-asset-pack, aus `Actor/Character/NinjaBlue/SpriteSheet.png` und `Actor/Character/RedNinja3/SpriteSheet.png` | Update #8 (Animation V2), 2026-03-29 | CC0 1.0 | abgeleitet mit `client/scripts/import-sprites.mjs`: nur die Farben von NinjaBlue, die sich an jedem Pixel von RedNinja3 unterscheiden (1512 Pixel, Originalfarben #79b8ce, #5f7160, #f2eaf1), in Graustufen nach Rec. 601 mit hellster Stufe 255, Zellen 48×48 |
