# ADR 0015 – Spielwerte sind einstellbar

**Status:** akzeptiert (2026-11-13)

## Kontext

Mit 012–016 kommen viele Zahlen ins Spiel: HP, Mana, Regeneration, Schaden, Reichweiten, Abklingzeiten,
Öffnungswinkel für Angriffe. Sie werden sich beim Ausbalancieren oft ändern. Bisher stehen sie als `const` in
`GameSettings`; manche Logik liest sie direkt, andere (`World`) bekommt sie übergeben. Einzelne Werte (Nachlauf,
Speicherintervall) lassen sich schon per Konfiguration überschreiben. Der Client bekommt Tempo und Max-Werte im
`Welcome`.

## Entscheidung

Neuer Grundsatz 8 in der [Constitution](../../specs/constitution.md): Jeder Wert, der Spielverhalten festlegt, ist
benannt, steht zentral und wird der Spiellogik übergeben. Der Client bekommt solche Werte vom Server. Der
Constitution-Check im Plan-Template prüft das.

Wie die Werte konfiguriert werden (Konfigurationsdatei, Admin-Interface, zur Laufzeit), entscheidet eine eigene
Spec. Bis dahin genügen die Standardwerte in `GameSettings`.

## Konsequenzen

- Neue Pläne führen jeden Spielwert mit Standard in ihrer Konstanten-Tabelle; die Domain-Logik nimmt ihn als
  Parameter.
- Bestehender Code wird angepasst, wenn sein Feature wieder angefasst wird; die Werte stehen bereits gesammelt in
  `GameSettings`.
- Der spätere Schritt zur Konfiguration ist nur Verdrahtung, kein Umbau der Logik.

## Alternativen

- **Sofort alles konfigurierbar machen:** Konfigurationsweg und Admin-Oberfläche ohne konkreten Bedarf; widerspricht
  KISS (Grundsatz 5).
- **Werte als Konstanten in der Logik lassen:** Jede Balance-Änderung wäre ein Code-Umbau quer durch Domain und
  Client.
