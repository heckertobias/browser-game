# 001 – Walking Skeleton

**Status:** entwurf

## Ziel

Der dünnste mögliche Durchstich durch alle Schichten: Zwei Spieler öffnen das Spiel im Browser,
sehen eine Spielfläche und sehen sich gegenseitig herumlaufen. Kein Kampf, keine Grafik-Assets,
kein Offline-Teil – nur der Beweis, dass Client, Server und Echtzeit-Verbindung zusammenspielen.

## User Stories

- Als **Spieler** möchte ich das Spiel über einen Link öffnen und sofort eine Figur steuern können.
- Als **Spieler** möchte ich andere Spieler in Echtzeit herumlaufen sehen.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Startseite, wenn ich auf „Spielen“ klicke, dann sehe ich eine Spielfläche mit meiner Figur.
- **AK-2:** Gegeben ich bin im Spiel, wenn ich die Bewegungstasten drücke, dann bewegt sich meine Figur frei (nicht in Kacheln).
- **AK-3:** Gegeben zwei Spieler sind im Spiel, wenn einer sich bewegt, dann sieht der andere die Bewegung innerhalb von ca. einer Sekunde.
- **AK-4:** Gegeben ein Spieler schließt den Tab, dann verschwindet seine Figur bei den anderen.
- **AK-5:** Gegeben ein Spieler versucht, sich schneller als erlaubt zu bewegen, dann wird er vom Server auf eine gültige Position zurückgesetzt.

## Out of Scope

- Login / Accounts (Spieler sind anonym, Name zufällig)
- Speichern der Position nach dem Verlassen
- Kampf, NPCs, Karte mit Hindernissen
- Pixel-Art – Figuren dürfen farbige Quadrate sein

## Offene Fragen

- [ ] Steuerung: WASD, Pfeiltasten, Klick-zum-Laufen oder mehrere davon?
- [ ] Wie groß ist die Spielfläche – ein Bildschirm oder scrollende Kamera?
