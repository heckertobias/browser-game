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
- Bewegung: frei, nicht tile-basiert
- Kampf: Tab-Targeting
- Öffentliche Seiten (Landing) serverseitig gerendert

## Offene Fragen

- [ ] Ist der Offline-Fortschritt **nur persönlich** (z. B. Ressourcen, Produktion) oder **verändert sich die Welt** (Gebiete, Fraktionen)?
- [ ] Was genau läuft offline weiter? (Beispiele: Gebäude, Begleiter, Handwerk, Expeditionen)
- [ ] Wie viele Spieler sollen sich realistisch gleichzeitig in einem Gebiet aufhalten?
- [ ] Setting / Thema der Welt?
- [ ] UI-Stil: wie stark Pixel-Look in Menüs und HUD?

## Nicht-Ziele (vorerst)

- Mobile-App
- Mehrere Server/Shards
- Monetarisierung
