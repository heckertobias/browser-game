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
