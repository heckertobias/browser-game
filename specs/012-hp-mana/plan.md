# 012 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Server  ── World (Domain): Player.Hp / Player.Mana, Regeneration in Step(), Damage(), SetVitals(), Respawn()
        ── GameLoop: Max-Werte im Welcome, eigene HP/Mana im Snapshot (pro Empfänger), spawn_seq je Figur
Client  ── net.ts / game.ts: Werte aus Welcome und Snapshot → GameEvents.onVitals (nur bei Änderung)
        ── features/game/hud/Vitals.tsx: zwei Balken mit Zahl im HUD
        ── interpolation.ts: nach Respawn (spawn_seq geändert) springen statt gleiten
```

HP und Mana leben nur im Speicher des Servers, an der Figur in der `World`. Es gibt keine Persistenz (Out of Scope).

## Konstanten (`GameSettings`)

| Name | Wert | Bedeutung |
|---|---|---|
| `MaxHp` | 100 | Max-HP (AK-1) |
| `MaxMana` | 100 | Max-Mana (AK-1) |
| `ManaRegenPerSecond` | 5 | Mana pro Sekunde; leer → voll in 20 s (AK-3) |

Pro Tick regeneriert `ManaRegenPerSecond / TickRateHz` (bei 20 Hz 0,25). Die Werte sind `double`; der HUD zeigt
abgerundete ganze Zahlen.

## Entschiedene Fragen (Plan)

1. **„Anmeldung“ heißt neue Figur.** HP und Mana sind voll, wenn eine Figur neu in die `World` kommt (Anmeldung,
   Abmelden + Anmelden, Server-Neustart, Nachlauf abgelaufen). Übernimmt eine Verbindung eine **wartende** Figur
   (Netz kurz weg, 003 AK-21, oder ein anderes Gerät, 003 AK-12), behält sie ihre Werte. Sonst ließe sich HP durch kurzes
   Trennen auffüllen.
2. **Respawn = neuer Startpunkt wie beim ersten Start** (`World.AddPlayer` ohne gespeicherte Position: zufällig im
   `SpawnRadius` um (0,0)). Die gehaltene Richtung und offene Eingaben werden verworfen, damit die Figur nicht sofort
   weiterläuft.
3. **Sprünge sichtbar machen:** `PlayerState.spawn_seq` zählt jeden Sprung (Respawn; später auch `resetPosition()` aus
   015). Ändert er sich, interpoliert der Client nicht zwischen alter und neuer Position, sondern springt. Die eigene
   Figur korrigiert die Prediction ohnehin auf die Server-Position.
4. **Eigene Werte im Snapshot, Max-Werte im Welcome.** Andere Spieler sehen fremde HP in 012 nicht (Spec zeigt nur den
   eigenen HUD); das spart Bandbreite.
5. **Regeneration im Tick** und nicht aus Zeitstempeln berechnet: Mana wächst nur, solange die Figur in der Welt ist.
   Constitution §4 betrifft Fortschritt, während niemand spielt; den gibt es hier nicht.
6. **Wartende Figuren regenerieren weiter** (sie sind in der Welt). Das ist einfacher als eine Ausnahme und hat keine
   Wirkung auf andere.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Feldnummern sind über die Pläne 002 und
011–016 abgestimmt: `Welcome` 6–7, `Snapshot` 4–5, `PlayerState` 7.

```proto
message Welcome {
  // … 1–4 wie bisher, 5: key_bindings (002)
  float max_hp = 6;
  float max_mana = 7;
}

message Snapshot {
  uint32 tick = 1;
  uint32 last_processed_seq = 2;
  repeated PlayerState players = 3;
  // Pro Empfänger: eigene aktuelle Werte (012).
  float hp = 4;
  float mana = 5;
}

message PlayerState {
  // … 1–6 wie bisher
  // Zählt Sprünge der Figur (Respawn, Zurücksetzen); ändert er sich, nicht interpolieren.
  uint32 spawn_seq = 7;
}
```

**OpenAPI:** keine Änderung.

## Server

### `World` (Domain, ohne Netz und DB)
- `Player`: `Hp`, `Mana` (`double`), `SpawnSeq` (`uint`). Beim Anlegen `Hp = MaxHp`, `Mana = MaxMana` (AK-2).
- `World` bekommt `maxHp`, `maxMana`, `manaRegenPerSecond` im Konstruktor (Tests setzen eigene Werte).
- `Step()`: für jede Figur `Mana = min(MaxMana, Mana + ManaRegenPerSecond / TickRateHz)` (AK-3). HP unverändert (AK-4).
- `Damage(playerId, amount)`: `amount ≤ 0` oder unbekannte Figur → nichts. `Hp -= amount`; `Hp ≤ 0` → `Respawn`
  im selben Aufruf (AK-7, AK-9). Rückgabe: ob die Figur gestorben ist (für Tests und 014).
- `SetVitals(playerId, hp?, mana?)`: setzt die Werte, auf `[0, Max]` begrenzt; `hp ≤ 0` → Respawn (AK-10). Das ist der
  serverseitige Zugang für Tests und später für `fullHealth()` (015).
- `Respawn(player)`: neue Startposition wie `AddPlayer`, `Hp`/`Mana` voll, `SpawnSeq++`, Richtung und offene Eingaben
  verwerfen (AK-8). Verworfene Eingaben gelten als verarbeitet (`LastProcessedSeq = LastQueuedSeq`), damit der Client
  sie nicht auf die neue Position anwendet.

### `GameLoop`
- `Welcome`: `MaxHp`, `MaxMana`.
- `BroadcastSnapshot`: `spawn_seq` je Figur; pro Empfänger `hp` und `mana` der eigenen Figur.

## Client

- **`net.ts`**: unverändert bis auf die neuen Felder (generiert).
- **`game.ts`**: `GameEvents.onVitals({ hp, maxHp, mana, maxMana })`, nur wenn sich die abgerundeten Werte ändern
  (wie `onCoordinates`). Max-Werte aus `Welcome`.
- **`features/game/hud/Vitals.tsx`** (HUD-Ebene, oben links unter Name/Status, Safe Areas):
  - zwei schmale Balken (HP rot, Mana blau) mit Text `HP 73/100` bzw. `MP 40/100` (AK-5, AK-6).
  - `vitalsText(value, max)` und `fillPercent(value, max)` als reine Funktionen.
- **`interpolation.ts`**: `RemotePlayer.spawnSeq`; ist er im späteren Snapshot anders als im früheren, wird die spätere
  Position ohne Zwischenwerte genommen.
- **`prediction.ts`**: keine Änderung (`reconcile` übernimmt die Server-Position; ausstehende Eingaben sind wenige Ticks
  und werden darauf angewandt).
- **`i18n.ts`**: `hp`, `mana` (de/en; Abkürzungen „HP“/„MP“ bleiben in beiden Sprachen gleich, aber als Schlüssel).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `Player.Hp/Mana`, begrenzt in `SetVitals`/`Step` | Unit: Werte bleiben in `[0, Max]` (SetVitals mit −5 und 500) |
| 2 | Neue Figur voll; Übernahme behält | Unit: `AddPlayer` → voll; Integration (WS): `Welcome` + erster Snapshot voll; Übernahme einer wartenden Figur behält gesenkte HP |
| 3 | Regeneration in `Step()` | Unit: Mana 0 → nach 20 Ticks +5; nie über Max |
| 4 | keine HP-Regeneration | Unit: HP 50 → nach 100 Ticks 50 |
| 5 | `Vitals.tsx` | Vitest: `vitalsText`, `fillPercent`; **manuell** |
| 6 | Snapshot pro Tick, `onVitals` bei Änderung | Integration (WS): `SetVitals` → nächster Snapshot hat neue Werte; **manuell:** Mana-Balken füllt sich |
| 7 | `Damage` | Unit: 100 − 30 = 70; 20 − 30 → tot |
| 8 | `Respawn` | Unit: nach Tod Position im `SpawnRadius`, HP/Mana voll, `SpawnSeq + 1`, Richtung 0; Vitest: Interpolation springt bei neuem `spawnSeq` |
| 9 | Respawn im selben Aufruf | Unit: direkt nach `Damage` lebt die Figur mit vollen Werten, kein Zwischenzustand |
| 10 | `SetVitals` | Unit-Tests oben laufen ohne Gegner |

## Constitution-Check

- [x] **Server ist Autorität:** HP, Mana, Regeneration und Respawn rechnet nur der Server; der Client zeigt an.
- [x] **Typen nur aus `contracts/`:** neue Felder in Protobuf.
- [x] **Offline-Fortschritt:** nicht betroffen (keine Persistenz; siehe Entscheidung 5).
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS:** zwei Zahlen an der Figur, ein Zähler für Sprünge.

## Risiken / Abweichungen

- **Interpretation von AK-2** (Entscheidung 1): Übernahme einer wartenden Figur gilt nicht als neue Anmeldung. Falls
  das anders gemeint ist, ist es eine Zeile in `AddOrTakeOver`.
- **Respawn-Ort:** Der Startpunkt ist zufällig im `SpawnRadius`; zwei Respawns landen also nicht am selben Pixel. Das
  entspricht 001/003.
- **Prediction beim Respawn:** Eingaben, die beim Respawn schon unterwegs sind, verarbeitet der Server danach normal;
  die Figur läuft also höchstens ein RTT lang in die alte Richtung weiter, wenn der Spieler die Taste hält.
