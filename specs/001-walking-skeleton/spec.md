# 001 – Walking Skeleton

**Status:** bereit

## Ziel

Der dünnste mögliche Durchstich durch alle Schichten: Zwei Spieler öffnen das Spiel im Browser,
sehen eine Spielfläche und sehen sich gegenseitig herumlaufen. Kein Kampf, keine Grafik-Assets,
kein Offline-Teil – nur der Beweis, dass Client, Server und Echtzeit-Verbindung zusammenspielen.

## User Stories

- Als **Spieler** möchte ich das Spiel über einen Link öffnen und sofort eine Figur steuern können.
- Als **Spieler** möchte ich andere Spieler in Echtzeit herumlaufen sehen.
- Als **Spieler** möchte ich mit Tastatur, Maus oder Finger laufen können, je nachdem, woran ich gerade sitze.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Startseite, wenn ich auf „Spielen“ klicke, dann sehe ich eine Spielfläche mit meiner Figur.
- **AK-2:** Gegeben ich bin im Spiel, wenn ich W, A, S oder D drücke, dann bewegt sich meine Figur frei (nicht in Kacheln)
  in die entsprechende Richtung; diagonal (zwei Tasten) ist sie genauso schnell wie geradeaus.
- **AK-2b:** Gegeben ich bin im Spiel, wenn ich Maustaste oder Finger auf der Spielfläche gedrückt halte, dann läuft meine
  Figur in Richtung des Zeigers und folgt ihm, solange ich gedrückt halte; lasse ich los, bleibt sie stehen.
- **AK-3:** Gegeben zwei Spieler sind im Spiel, wenn einer sich bewegt, dann sieht der andere die Bewegung innerhalb von ca. einer Sekunde.
- **AK-4:** Gegeben ein Spieler schließt den Tab, dann verschwindet seine Figur bei den anderen.
- **AK-5:** Gegeben ein Spieler versucht, sich schneller als erlaubt zu bewegen, dann wird er vom Server auf eine gültige Position zurückgesetzt.
- **AK-6:** Gegeben ich bewege mich, dann folgt die Kamera meiner Figur. Gegeben zwei Spieler mit unterschiedlich großen
  Bildschirmen, dann sehen beide einen gleich großen Ausschnitt der Welt; passt das Seitenverhältnis des Bildschirms nicht,
  bleiben Ränder frei, statt mehr Welt zu zeigen.
- **AK-7:** Gegeben ich laufe in eine beliebige Richtung, dann stoße ich auf keine Grenze, und ein Hintergrundmuster
  lässt mich erkennen, dass ich mich bewege.

## Out of Scope

- Login / Accounts (Spieler sind anonym, Name zufällig)
- Speichern der Position nach dem Verlassen
- Kampf, NPCs, Karte mit Hindernissen oder Weltgrenzen
- Tasten umbelegen (→ [002 – Tastenbelegung](../002-keybindings/spec.md))
- Pixel-Art – Figuren dürfen farbige Quadrate sein

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Steuerung: WASD sowie Gedrückt-halten-und-folgen mit Maus oder Finger. Freie Tastenbelegung kommt mit 002.
- Spielfläche: keine Grenzen; die Kamera folgt der Figur, alle sehen einen gleich großen Ausschnitt.
