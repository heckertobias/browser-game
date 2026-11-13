# 013 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── input.ts            Tippen/Klicken auf ein Ziel wählt es aus (statt Laufen); Tasten 1/2 lösen Angriffe aus
        ── features/game/combat/targeting.ts  reine Logik: Treffer-Test, aktives Ziel, Ziel weg → aufheben
        ── features/game/combat/AttackButtons.tsx  zwei HUD-Buttons „Nah“ / „Fern“ (Touch und Maus)
        ── render.ts           Dummy, HP-Balken, Ziel-Umrandung, Projektile
        ── net.ts / game.ts    Attack schicken, Ziele + Projektile aus dem Snapshot, Hinweis „zu wenig Mana“
Server  ── GameEndpoint: Attack → Angriffs-Channel der Verbindung
        ── GameLoop: Angriffe im Tick prüfen und ausführen, Ziele + Projektile in den Snapshot
        ── Features/Game/Combat/Domain/: Combat (Regeln), Target (zielbares Objekt mit HP), Projectile
```

Die Kampfregeln sind reine Logik ohne Netzwerk und DB (Constitution §7). Ziele und Projektile leben nur im Speicher
(AK-17, kein Speichern).

## Konstanten (`GameSettings`)

| Name | Wert | Bedeutung |
|---|---|---|
| `AttackArcDegrees` | 90 | Öffnungswinkel nach vorn (± 45° um die Blickrichtung aus 016), für beide Angriffe |
| `MeleeRange` | 32 | Nahkampfreichweite (Mittelpunkt zu Mittelpunkt, Welteinheiten) |
| `MeleeDamage` | 10 | Nahkampfschaden |
| `RangedRange` | 160 | Maximalreichweite Fernkampf (knapp die halbe kurze Bildschirmseite) |
| `RangedDamage` | 15 | Fernkampfschaden |
| `RangedManaCost` | 20 | Mana je Fernkampf (bei 012: 5 Schüsse aus vollem Mana, dann alle 4 s einer) |
| `ProjectileSpeed` | 400 | Welteinheiten pro Sekunde (volle Reichweite in 0,4 s) |
| `GlobalCooldownSeconds` | 1 | allgemeiner Cooldown (Spec: 1 Sekunde) |
| `DummyPosition` | (0, −128) | fest, außerhalb des Spawn-Radius (64), auf dem Bildschirm beim Start sichtbar |
| `DummyMaxHp` | 100 | Start-HP |
| `DummyRespawnSeconds` | 5 | Verzögerung bis zur Wiederherstellung |
| `TargetHitRadius` | 12 | Klick-/Tipp-Radius um ein Ziel (Client) |

## Entschiedene Fragen (Plan)

1. **Das Ziel kennt nur der Client.** Er schickt mit jedem Angriff die Ziel-ID mit; der Server prüft, ob es das Ziel
   gibt und ob es lebt. Damit braucht es keine Nachricht „Ziel gewählt“ und keinen Zustand auf dem Server. Auswahl,
   Hervorhebung und „höchstens ein Ziel“ (AK-1, AK-3) sind reine Anzeige.
2. **Ein Ziel, das im Snapshot fehlt, ist weg.** Ein toter Dummy wird bis zur Wiederherstellung nicht übertragen und
   nicht gezeichnet. Der Client hebt die Auswahl auf, sobald die ID fehlt (Spec: „stirbt er, wird das Ziel aufgehoben“).
3. **Reihenfolge der Prüfungen:** Ziel gültig → Cooldown abgelaufen → in Reichweite → in Blickrichtung →
   (Fernkampf) genug Mana. Ein
   Versuch während des Cooldowns wird still ignoriert. „Zu wenig Mana“ kommt also erst, wenn sonst alles passt, und
   flackert nicht bei jedem Tastendruck im Cooldown.
4. **Nur „zu wenig Mana“ wird gemeldet** (`AttackRejected`, AK-8). Alles andere scheitert still (AK-2, AK-5, AK-9,
   AK-19);
   die Spec verlangt dafür keine Rückmeldung.
5. **Projektile verfolgen ihr Ziel** (Ziele stehen in 013 und 014 ohnehin still). Pro Tick bewegt sich das Projektil
   `ProjectileSpeed / TickRateHz` auf das Ziel zu; ist der Rest kürzer, schlägt es ein. Ist das Ziel inzwischen weg
   (tot, besiegt), verschwindet das Projektil ohne Wirkung. Die Reichweite wird nur beim Start geprüft (AK-9).
6. **Cooldown in Ticks**, gemessen ab dem Tick des erfolgreichen Angriffs (`NextAttackTick = tick + 20`).
7. **Pointer:** `pointerdown` auf ein Ziel (innerhalb `TargetHitRadius`, nächstes gewinnt) wählt es aus und startet
   **kein** Halten-und-Folgen. Daneben läuft man wie bisher; die Auswahl bleibt dabei bestehen.
8. **Tasten:** zwei neue Aktionen für die Tastenbelegung aus 002: `ACTION_ATTACK_MELEE` (Standard `Digit1`) und
   `ACTION_ATTACK_RANGED` (Standard `Digit2`). Sie lösen bei `keydown` einmal aus (ohne Auto-Repeat), nicht solange die
   Taste gedrückt ist. Ohne Taste belegt sind sie erlaubt (002 AK-11 gilt nur für Bewegung).
9. **Buttons im HUD:** „Nah“ und „Fern“ unten rechts über der Versionsanzeige, groß genug für Daumen (`min-h-14`),
   auch am PC sichtbar (mit Maus klickbar). Ohne Ziel sind sie ausgegraut, lösen aber nichts aus (AK-2).
10. **Blickrichtung (AK-19):** Der Winkel zwischen `Player.Facing` (016, Server) und dem Vektor Angreifer → Ziel
    darf höchstens `AttackArcDegrees / 2` betragen; genau auf der Grenze zählt als vorn. Steht das Ziel auf der
    Position des Angreifers (Abstand 0), zählt es als vorn. Der Server dreht die Figur nicht.
11. **Ziel-Modell allgemein:** `Target` (ID, Art, Position, HP, Max-HP) im Domain-Code; 014 hängt Gegner als weitere
    Art ein. Spieler sind keine Ziele (Out of Scope).

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Feldnummern sind über die Pläne 002 und
011–016 abgestimmt: `ClientMessage` 4, `ServerMessage` 7, `Snapshot` 6–7, `Action` 5–6.

```proto
message ClientMessage {
  oneof payload {
    Input input = 1;
    // 2: SendChat (011), 3: SaveKeyBindings (002)
    Attack attack = 4;
  }
}

enum Action {
  // … 0–4 aus 002
  ACTION_ATTACK_MELEE = 5;
  ACTION_ATTACK_RANGED = 6;
}

// Angriff auf ein Ziel. Der Server prüft Ziel, Cooldown, Reichweite und Mana.
message Attack {
  enum Kind {
    KIND_UNSPECIFIED = 0;
    KIND_MELEE = 1;
    KIND_RANGED = 2;
  }
  Kind kind = 1;
  string target_id = 2;
}

message ServerMessage {
  oneof payload {
    // … 1–3, 4–6: Chat (011)
    // Nur an den Angreifer: Angriff nicht ausgeführt.
    AttackRejected attack_rejected = 7;
  }
}

message AttackRejected {
  enum Reason {
    REASON_UNSPECIFIED = 0;
    // Zu wenig Mana für den Fernkampf (AK-8).
    REASON_NOT_ENOUGH_MANA = 1;
  }
  Reason reason = 1;
}

message Snapshot {
  // … 1–3, 4–5: eigene HP/Mana (012)
  // Lebende Ziele (013: Trainings-Dummy; 014: Gegner). Fehlt eine ID, ist das Ziel weg.
  repeated TargetState targets = 6;
  repeated ProjectileState projectiles = 7;
}

message TargetState {
  enum Kind {
    KIND_UNSPECIFIED = 0;
    KIND_TRAINING_DUMMY = 1;
  }
  string id = 1;
  Kind kind = 2;
  float x = 3;
  float y = 4;
  float hp = 5;
  float max_hp = 6;
}

message ProjectileState {
  uint32 id = 1;
  float x = 2;
  float y = 3;
}
```

**OpenAPI:** keine Änderung.

## Server

### Domain (`Features/Game/Combat/Domain/`)
- `Target` (`Id`, `Kind`, `X`, `Y`, `Hp`, `MaxHp`, `RespawnAtTick?`). `IsAlive => RespawnAtTick is null && Hp > 0`.
- `Projectile` (`Id`, `X`, `Y`, `TargetId`, `Damage`).
- `Combat` (gehört zur `World`, wird im Tick-Thread benutzt):
  - Konstruktor mit den Konstanten (Tests setzen eigene); legt den Dummy `"dummy"` an `DummyPosition` mit vollen HP an
    (AK-13, AK-17: lebt nur im Speicher, neu bei Serverstart).
  - `TryAttack(Player attacker, Attack.Kind kind, string targetId, uint tick) → AttackResult`
    (`Ok`, `InvalidTarget`, `Cooldown`, `OutOfRange`, `NotFacing`, `NotEnoughMana`) nach der Reihenfolge oben.
    - Nahkampf: Schaden sofort (AK-4, AK-6).
    - Fernkampf: Mana abziehen (AK-10), Projektil an der Position des Angreifers starten (AK-7).
    - Erfolg setzt `attacker.NextAttackTick = tick + GlobalCooldownTicks` (AK-11); Fehler nicht (AK-12).
  - `Step(tick)`: Projektile bewegen, einschlagen lassen (AK-7, AK-14); Ziele mit 0 HP bekommen
    `RespawnAtTick = tick + DummyRespawnTicks`; fällig → volle HP (AK-15).
  - `Damage(target, amount)`: `Hp = max(0, Hp − amount)`.
  - Der Dummy greift nie an (AK-16): Er hat keinerlei Angriffslogik.
- `Player.NextAttackTick` (`uint`).

### Spiel (`Features/Game/`)
- `GameEndpoint`: `Attack` → `connection.Attacks.Writer.TryWrite(…)` (eigener kleiner Channel, Kapazität 4,
  `DropOldest`).
- `GameLoop.Tick()`: nach `World.Step()` (Positionen aktuell) die Angriffs-Channels aller Verbindungen leeren und
  `Combat.TryAttack` aufrufen; `NotEnoughMana` → `AttackRejected` an den Angreifer. Danach `Combat.Step(tick)`, dann
  Snapshot mit `targets` (nur lebende) und `projectiles`.

## Client

- **`input.ts`:**
  - `pointerdown` fragt über einen Callback `pickTarget(clientX, clientY)` nach einem Ziel. Gibt es eins, wird kein
    Halten gestartet (Entscheidung 7).
  - `keydown` ohne `e.repeat`: gebundene Angriffstaste → Callback `onAttack(kind)` (Entscheidung 8). Angriffsaktionen
    zählen nicht zur Laufrichtung.
- **`features/game/combat/targeting.ts`** (Vitest):
  - `pickTarget(world, targets, radius)`: nächstes Ziel innerhalb des Radius oder `null`.
  - `TargetSelection`: `select(id)`, `current`, `sync(ids)` hebt auf, wenn die ID fehlt (AK-3, Entscheidung 2).
  - `attackRequest(selection, kind)`: ohne Ziel `null` → es wird nichts geschickt (AK-2).
- **`game.ts`:** Ziele aus dem Snapshot (ohne Interpolation, sie stehen still), Projektile mit derselben Interpolation
  wie andere Spieler; `attack(kind)` für Tasten und Buttons; `GameEvents.onTarget(id | null)` und
  `onAttackRejected(reason)`.
- **`render.ts`:**
  - Dummy: brauner Pfosten mit Querbalken (deutlich anders als die farbigen Spieler-Quadrate), HP-Balken darüber
    (AK-18).
  - Aktives Ziel: gelbe Umrandung (AK-1).
  - Projektil: kleiner heller Kreis.
- **`AttackButtons.tsx`:** zwei Buttons (Entscheidung 9). Hinweis „Zu wenig Mana“ kurz (2 s) über den Buttons (AK-8).
- **Tastenbelegung (002):** `DEFAULT_BINDINGS` um `Digit1`/`Digit2` ergänzen, Aktionsnamen in i18n. `fromProto` aus 002
  gibt älteren gespeicherten Belegungen die neuen Standardtasten, sofern frei.
- **`i18n.ts`:** `attackMelee`, `attackRanged`, `notEnoughMana`, `actionAttackMelee`, `actionAttackRanged` (de/en).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `pickTarget`, Umrandung | Vitest: Treffer im Radius, nächstes gewinnt, daneben `null`; **manuell:** Klick und Tippen |
| 2 | `attackRequest` ohne Ziel `null`; Server `InvalidTarget` | Vitest; Unit: unbekannte ID → `InvalidTarget`, kein Cooldown |
| 3 | `TargetSelection` | Vitest: zweite Auswahl ersetzt die erste |
| 4 | Nahkampf in Reichweite | Unit: Abstand 32 → Schaden 10 |
| 5 | Nahkampf außer Reichweite | Unit: Abstand 33 → `OutOfRange`, HP gleich |
| 6 | kein Mana-Abzug | Unit: Mana vorher = nachher |
| 7 | Projektil, Schaden bei Einschlag | Unit: Start → nach n Ticks Einschlag, erst dann Schaden; Integration (WS): Angriff → Projektil im Snapshot, danach Dummy-HP gesenkt |
| 8 | `NotEnoughMana`, `AttackRejected`, Hinweis | Unit: Mana 19 → abgelehnt, Mana 19, kein Projektil; Integration: nur Angreifer bekommt `AttackRejected` |
| 9 | Reichweite beim Start | Unit: Abstand 161 → `OutOfRange` |
| 10 | Mana-Abzug | Unit: Mana 100 → 80 |
| 11 | `NextAttackTick` | Unit: zweiter Angriff im selben Takt → `Cooldown`; nach 20 Ticks ok; gilt über Angriffsarten hinweg |
| 12 | Fehler setzt keinen Cooldown | Unit: `OutOfRange`, dann gültiger Angriff im nächsten Tick → `Ok` |
| 13 | Dummy beim Start | Unit: `Combat` neu → Dummy an `DummyPosition` mit `DummyMaxHp`; Integration: erster Snapshot enthält ihn |
| 14 | `Damage` | Unit (siehe 4, 7) |
| 15 | `RespawnAtTick` | Unit: 0 HP → fehlt in lebenden Zielen; nach 100 Ticks wieder da, volle HP; Projektil auf toten Dummy verfällt |
| 16 | keine Angriffslogik | Unit: Spieler neben Dummy, 100 Ticks → Spieler-HP unverändert |
| 17 | nur im Speicher | Integration: neuer Server → Dummy mit vollen HP |
| 18 | HP-Balken | Vitest: `hpBarWidth`; **manuell** |
| 19 | Winkel zur Blickrichtung | Unit: Ziel genau vorn, auf ± 45° → `Ok`; auf 46° und hinter dem Spieler → `NotFacing`, HP, Mana und Cooldown unverändert; Abstand 0 → `Ok`; gilt für Nah- und Fernkampf |

## Constitution-Check

- [x] **Server ist Autorität:** Der Client schickt nur „Angriff X auf Ziel Y“. Ziel-Gültigkeit, Reichweite, Mana,
  Cooldown, Projektilflug, Schaden und Respawn rechnet der Server.
- [x] **Typen nur aus `contracts/`:** neue Nachrichten in Protobuf.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS / kleiner Durchstich:** ein Ziel, zwei Angriffe, keine Server-Auswahl, kein Kampf-Log.

## Risiken / Abweichungen

- **Abhängigkeit von 002:** Die Angriffstasten nutzen die Aktionen aus 002. Kommt 013 vor 002, stehen sie in
  `DEFAULT_BINDINGS` (die gibt es schon) und das Enum entsteht mit 013; die Nummern bleiben gleich.
- **Reichweite und Verzögerung:** Der Server prüft die Reichweite mit seiner Position, der Spieler sieht seine
  vorhergesagte. Am Rand der Reichweite kann ein Angriff daher scheitern, obwohl es knapp passend aussah. Bei 50 ms
  Ticks und 200 Einheiten/s sind das wenige Welteinheiten.
- **Blickrichtung und Verzögerung:** Der Server prüft mit seiner Blickrichtung, der Client zeigt die vorhergesagte
  (016). Wer sich gerade erst zum Ziel gedreht hat, kann knapp scheitern; ± 45° lassen dafür genug Spielraum.
- **Viele Projektile:** Pro Spieler höchstens eines pro Sekunde (Cooldown); kein eigenes Limit nötig.
- **Klick auf ein Ziel startet keine Bewegung:** Wer genau auf den Dummy tippt, um hinzulaufen, wählt ihn stattdessen
  aus. Laufen geht daneben oder mit Tasten. Um sich dem Ziel zuzuwenden (AK-19), tippt man knapp daneben in seine
  Richtung oder läuft mit Tasten einen Schritt darauf zu.
