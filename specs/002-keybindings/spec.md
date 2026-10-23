# 002 – Tastenbelegung

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

## Ziel

Spieler können jede Aktion auf die Taste legen, die ihnen passt – etwa für andere Tastaturlayouts,
Linkshänder oder Gewohnheiten aus anderen Spielen.

## User Stories

- Als **Spieler** möchte ich jede Aktion (z. B. Laufen nach oben/unten/links/rechts) auf eine Taste meiner Wahl legen,
  damit ich so spielen kann, wie es mir angenehm ist.
- Als **Spieler** möchte ich meine Belegung jederzeit auf den Standard zurücksetzen können.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Einstellungen, wenn ich eine Aktion auswähle und eine Taste drücke, dann löst diese Taste ab sofort die Aktion aus.
- **AK-2:** Gegeben ich belege eine Taste, die schon einer anderen Aktion zugeordnet ist, dann werde ich darauf hingewiesen,
  bevor etwas überschrieben wird.
- **AK-3:** Gegeben ich habe Tasten umbelegt, wenn ich das Spiel später erneut öffne, dann gilt meine Belegung weiterhin.
- **AK-4:** Gegeben ich habe Tasten umbelegt, wenn ich „Standard wiederherstellen“ wähle, dann gilt wieder WASD.

## Out of Scope

- Gamepad-Unterstützung
- Belegung von Maus- und Touch-Gesten

## Offene Fragen

- [ ] Gilt die Belegung pro Gerät/Browser oder pro Account (gibt es noch nicht)?
- [ ] Darf eine Aktion mehrere Tasten haben (z. B. W **und** Pfeil hoch)?
- [ ] Wo sind die Einstellungen erreichbar – nur im Spiel oder auch auf der Startseite?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
