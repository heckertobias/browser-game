# Constitution

Grundprinzipien, die für **jedes** Feature gelten. Jeder `plan.md` wird dagegen geprüft.
Änderungen hier nur bewusst und mit ADR.

## 1. Spec vor Code
Kein Feature ohne `spec.md` im Status `bereit`. Ändert sich das Verhalten, wird zuerst die Spec angepasst.

## 2. Der Server ist die Autorität
Der Client schlägt vor (Eingaben), der Server entscheidet (Position, Schaden, Loot, Zeit).
Nichts, was den Spielstand beeinflusst, wird dem Client geglaubt.

## 3. Verträge sind die einzige Typquelle
- HTTP: OpenAPI in `contracts/openapi/`
- WebSocket: Protobuf in `contracts/proto/`
Typen in C# und TypeScript werden daraus generiert, nie von Hand dupliziert.

## 4. Offline-Fortschritt wird berechnet, nicht simuliert
Was weiterläuft, während niemand spielt, wird beim nächsten Zugriff aus Zeitstempeln berechnet
(`jetzt − letzterStand`), statt im Hintergrund dauerhaft zu ticken. Ausnahme nur per ADR.

## 5. KISS
Die einfachste Lösung, die die Akzeptanzkriterien erfüllt. Keine Abstraktion auf Vorrat,
keine neue Abhängigkeit ohne konkreten Grund im Plan.

## 6. Kleine Durchstiche
Features werden so geschnitten, dass sie Ende-zu-Ende (Client → Server → Persistenz) laufen.
Lieber ein hässlicher, funktionierender Durchstich als eine schöne halbe Schicht.

## 7. Testbar ab Akzeptanzkriterium
Jedes Akzeptanzkriterium hat mindestens einen automatisierten Test oder eine dokumentierte manuelle Prüfung.
Spiellogik (Kampf, Bewegung, Offline-Berechnung) ist ohne Netzwerk und ohne Datenbank testbar.

## 8. Spielwerte sind einstellbar
Jeder Wert, der Spielverhalten festlegt (Schaden, Reichweite, Winkel, Tempo, Abklingzeit, HP, Kosten …), ist ein
benannter Wert an einer zentralen Stelle, nie eine Zahl in der Spiellogik. Er muss sich später konfigurieren lassen,
ohne die Logik umzubauen.
- Die Spiellogik bekommt die Werte übergeben (z. B. im Konstruktor), statt sie selbst zu lesen. Tests setzen eigene.
- Braucht der Client einen solchen Wert, bekommt er ihn vom Server (z. B. im `Welcome`), statt ihn zu kopieren.
- Die Spec nennt keine Zahlen; der Plan führt jeden Wert mit Standard in seiner Tabelle.
- Nicht gemeint: reine Darstellung und Bedienung (Farben, Pixelgrößen, Tipp-Radius) und technische Grenzen
  (Nachrichtengröße, Channel-Kapazität).
Wo und wie die Werte konfiguriert werden (Datei, Admin-Interface), regelt eine eigene Spec.
