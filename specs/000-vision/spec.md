# 000 – Vision

**Status:** entwurf

## Was ist das Spiel?

Ein 2D-Pixel-Art-MMORPG im Browser mit zwei Seiten:

1. **Online-Teil:** Eine frei begehbare Welt, in der sich Spieler in Echtzeit bewegen, sich sehen
   und mit Tab-Targeting kämpfen.
2. **Browsergame-Teil:** Das Spiel läuft weiter, wenn man nicht online ist – man kommt zurück
   und es ist etwas passiert.

## Säulen

| Säule | Bedeutung |
|---|---|
| Gemeinsame Welt | Andere Spieler sind sichtbar und relevant |
| Freie Bewegung | Keine Tiles beim Laufen, flüssige Steuerung |
| Fortschritt auch offline | Wer nicht da ist, verliert nicht den Anschluss |
| Läuft im Browser | Kein Download, Link genügt |

## Entschieden

- Grafik: 2D-Pixel-Art
- Setting: klassische Fantasy
- Bewegung: frei, nicht tile-basiert
- Kampf: Tab-Targeting
- Steuerung: Tastatur sowie Maus/Touch; jede Taste ist frei belegbar
- Sichtfeld: Alle Spieler sehen einen gleich großen Ausschnitt der Welt, egal wie groß ihr Bildschirm ist.
  Ein größerer oder schärferer Bildschirm zeigt dasselbe nur detaillierter, nicht mehr davon.
  Die Kamera folgt der eigenen Figur.
- Spielerzahl: bis zu ca. 50 Spieler gleichzeitig in einem Gebiet
- Offline-Fortschritt ist **nur persönlich**: Die Welt verändert sich durch das, was Spieler online tun –
  was offline weiterläuft, hat keinen Einfluss auf die Welt oder auf andere Spieler.
- Offline laufen weiter:
  - **Gebäude / Produktion** – eigene Gebäude erzeugen über die Zeit Ressourcen
  - **Begleiter / Expeditionen** – Begleiter werden losgeschickt und kommen mit Beute zurück
  - **Handwerk** – Herstellungsaufträge laufen in Echtzeit ab
  - Voraussichtlicher Mittelpunkt des Offline-Teils: **das Haus des Spielers** (Details folgen in einer eigenen Spec)
- Spielen nur mit Account (kein Gastmodus). Anmeldung per E-Mail + Passwort oder Passkey; der Spielername ist
  selbst gewählt und eindeutig.
- Einstellungen (z. B. Tastenbelegung) gehören zum Account und gelten auf jedem Gerät.
- Spiel verlassen: In der offenen Welt bleibt die Figur nach dem Gehen noch 20 Sekunden in der Welt (und kann dort
  später auch sterben); in sicheren Zonen kann man sich sofort abmelden. Wartende Figuren sind erkennbar
  (zunächst andere Farbe, später z. B. hinsetzen).
- UI-Stil: Pixel-Akzente – Menüs und HUD bleiben gut lesbar, mit pixeliger Schrift, Rahmen und Icons
- Öffentliche Seiten (Landing) serverseitig gerendert

## Offene Fragen

_Keine – neue Fragen hier sammeln, sobald sie auftauchen._

## Nicht-Ziele (vorerst)

- Mobile-App
- Mehrere Server/Shards
- Monetarisierung
