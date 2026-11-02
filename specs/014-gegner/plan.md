# 014 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Server  ── CommandRegistry (015): neuer Command spawnEnemy() → Combat.SpawnEnemy(vor dem Spieler, Blickrichtung 016)
        ── Combat (013): Gegner als weitere Art von Target; im Tick: Gegner greifen den nächsten Spieler in Reichweite an
        ── World.Damage (012): Schaden an Spielern, Tod → Respawn
        ── Snapshot: Gegner in targets (Kind ENEMY), besiegte fehlen
Client  ── render.ts: Gegner zeichnen (anders als Spieler und Dummy), HP-Balken wie beim Dummy
        ── Zielauswahl, Angriffe, HP-HUD: unverändert aus 012/013
```

Gegner sind zielbare Objekte aus 013 (`Target`) mit einer zweiten Art und eigener Angriffslogik. Fast alles Weitere
(Zielauswahl, Nah-/Fernkampf, Projektile, HP-Balken, Spieler-HP und Respawn) kommt unverändert aus 012, 013 und 015.

## Konstanten (`GameSettings`)

| Name | Wert | Bedeutung |
|---|---|---|
| `EnemyMaxHp` | 60 | Gegner-HP (6 Nahkampftreffer, 4 Fernkampftreffer aus 013) |
| `EnemyDamage` | 8 | Schaden je Treffer am Spieler (Spieler mit 100 HP stirbt nach 13 Treffern) |
| `EnemyRange` | 28 | Nahkampfreichweite des Gegners (kleiner als die Spieler-Nahkampfreichweite 32: Wer ganz nah ran muss, wird getroffen; Fernkampf aus Abstand ist sicher) |
| `EnemyCooldownSeconds` | 1,5 | Abklingzeit zwischen zwei Gegnertreffern |
| `EnemySpawnDistance` | 48 | Abstand vor dem Spieler beim Erzeugen (außerhalb von `EnemyRange`: Er greift nicht sofort an) |

## Entschiedene Fragen (Plan)

1. **Spawn-Punkt** = Position des Spielers + `EnemySpawnDistance` × (cos `Facing`, sin `Facing`) (AK-1, 016 AK-10).
   Keine Prüfung auf Überlappung mit anderen Figuren (kein Terrain, keine Kollision).
2. **IDs** `enemy-<n>` mit fortlaufendem Zähler im `Combat`; nie wiederverwendet, damit eine Auswahl im Client nicht
   versehentlich auf einen neuen Gegner springt.
3. **Wer wird angegriffen:** nur Spieler, die verbunden sind (nicht `away`). Wartende Figuren (Verbindung weg, 003
   AK-20) können sich nicht wehren; sie zu töten würde sie nach dem Wiederverbinden an den Startpunkt versetzen. Die Spec
   sagt „jeden Spieler in Reichweite“; wer nicht verbunden ist, gilt hier als nicht im Spiel.
4. **Nächster Spieler** (AK-6): kleinster Abstand Mittelpunkt zu Mittelpunkt; bei Gleichstand die kleinere Spieler-ID
   (deterministisch für Tests).
5. **Cooldown des Gegners** in Ticks, ab dem Treffer. Ein frisch erzeugter Gegner darf sofort angreifen, sobald jemand in
   Reichweite ist.
6. **Besiegt** (AK-10): Ein Gegner mit 0 HP wird im selben Tick entfernt, ohne Respawn. Projektile auf ihn verfallen (wie
   in 013). Im Snapshot fehlt er, der Client hebt die Auswahl auf (013).
7. **Reihenfolge im Tick:** Bewegung → Spieler-Angriffe (013) → Projektile → besiegte Gegner entfernen → Gegner greifen
   an → Snapshot. Ein im selben Tick besiegter Gegner schlägt also nicht mehr zu.
8. **Keine Obergrenze** (AK-2). Das Tempo-Limit des Chats (015) begrenzt das Erzeugen auf 5 in 10 s pro Account.
9. **Darstellung** (AK-11): rote Raute 16×16 mit dunklem Rand, HP-Balken wie beim Dummy. Spieler sind farbige Quadrate,
   der Dummy ist ein brauner Pfosten.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel: nur ein neuer Enum-Wert. Gegner nutzen
`TargetState` aus 013; Erzeugt wird per `SendChat` (011) mit `/run spawnEnemy()` (015).

```proto
message TargetState {
  enum Kind {
    KIND_UNSPECIFIED = 0;
    KIND_TRAINING_DUMMY = 1;
    KIND_ENEMY = 2;
  }
  // … Felder wie in 013
}
```

**OpenAPI:** keine Änderung.

## Server

### Domain (`Features/Game/Combat/Domain/`)
- `Target` bekommt `NextAttackTick` (nur für Gegner genutzt). Gegner haben kein `RespawnAtTick`.
- `Combat.SpawnEnemy(Player near) → Target`: Position nach Entscheidung 1, `EnemyMaxHp`, ID nach Entscheidung 2 (AK-1,
  AK-2, AK-4: Position wird danach nie geändert).
- `Combat.Step(tick, World world)` erweitert:
  - Gegner mit 0 HP entfernen (AK-10).
  - Für jeden Gegner mit `NextAttackTick ≤ tick`: nächsten verbundenen Spieler in `EnemyRange` suchen (AK-5, AK-6);
    keiner → nichts (AK-7). Sonst `world.Damage(player, EnemyDamage)` (012, inkl. Respawn bei 0 HP, AK-8) und
    `NextAttackTick = tick + EnemyCooldownTicks`.
- `Combat.TryAttack` (013) bleibt unverändert: Gegner sind Ziele wie der Dummy (AK-3, AK-9).

### Commands (`Features/Game/Commands/Application/`)
- `spawnEnemy` (0 Parameter) im `CommandRegistry` (015): ruft `Combat.SpawnEnemy(context.Self)`. `CommandContext`
  bekommt dafür `Combat`. Zugriff wie alle 015-Commands: keine Rechte, immer aktiv (AK-12). Keine Chat-Nachricht
  (015 AK-5, AK-1 hier).

### `GameLoop`
- Reihenfolge nach Entscheidung 7. Gegner kommen über die bestehende Liste `targets` in den Snapshot (`KIND_ENEMY`).

## Client

- **`render.ts`:** Zeichnen je `TargetState.Kind`: Dummy wie in 013, Gegner als rote Raute (Entscheidung 9),
  HP-Balken für beide (AK-11). Unbekannte Arten werden wie ein Gegner gezeichnet.
- **`targeting.ts`:** keine Änderung; `pickTarget` findet Gegner wie den Dummy (AK-3).
- Kein neuer HUD-Teil: Spieler-HP und Respawn zeigt der HUD aus 012.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `spawnEnemy` → `SpawnEnemy` vor dem Spieler | Unit: Spieler bei (0, 0), Blick π/2 → Gegner bei (0, 48); Blick 0 → (48, 0); Integration (WS): `/run spawnEnemy()` → Gegner im Snapshot, keine `ChatMessage` |
| 2 | ein Gegner je Aufruf, keine Obergrenze | Unit: 3 Aufrufe → 3 Gegner mit verschiedenen IDs |
| 3 | `Target` aus 013 | Unit: `TryAttack` mit Gegner-ID → `Ok` |
| 4 | Position wird nie geändert | Unit: 100 Ticks mit Spieler in der Nähe → Position gleich |
| 5 | Angriff mit Cooldown | Unit: Spieler in 28 → nach Tick HP 92; nächster Treffer erst nach 30 Ticks |
| 6 | nächster Spieler | Unit: zwei Spieler in Reichweite (10 und 20 entfernt) → nur der nähere verliert HP; Gleichstand → kleinere ID |
| 7 | niemand in Reichweite | Unit: Spieler bei 29 → keine HP-Änderung; wartender Spieler in Reichweite → keine HP-Änderung |
| 8 | `World.Damage` (012) | Unit: Spieler mit 8 HP → stirbt, Respawn am Startpunkt mit vollen HP; Gegner greift danach den nächsten an |
| 9 | Spieler-Angriffe aus 013 | Unit: Nahkampf auf Gegner → HP 50; Fernkampf → nach Einschlag 45 |
| 10 | Entfernen bei 0 HP | Unit: Gegner 0 HP → nicht mehr in `Targets`, kein Respawn nach 1000 Ticks; Projektil auf ihn verfällt; Integration: fehlt im Snapshot |
| 11 | Darstellung, HP-Balken | Vitest: Zeichenart je `Kind` (Gegner ≠ Dummy); **manuell** |
| 12 | Command-Modell aus 015 | Integration: frischer Account darf `spawnEnemy()` |

## Constitution-Check

- [x] **Server ist Autorität:** Spawn-Punkt, Reichweiten, Schaden, HP und Cooldowns rechnet der Server; der Client
  schickt nur den Befehl bzw. Angriffe aus 013.
- [x] **Typen nur aus `contracts/`:** ein neuer Enum-Wert.
- [x] **Offline-Fortschritt:** nicht betroffen (Gegner leben nur im laufenden Spiel).
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS:** keine Bewegung, kein Aggro-System, ein Angriff.

## Risiken / Abweichungen

- **Wartende Spieler** (Entscheidung 3) werden nicht angegriffen. Das ist eine Auslegung der Spec; falls gewünscht,
  entfällt einfach der `away`-Filter.
- **Viele Gegner:** Ohne Obergrenze kann die Welt viele Gegner sammeln (5 pro 10 s pro Account). Sie verschwinden erst,
  wenn sie besiegt werden oder der Server neu startet. Der Angriffs-Check ist O(Gegner × Spieler) pro Tick; bei Dutzenden
  unkritisch. Eine Obergrenze oder ein Aufräum-Command ist eine spätere Spec.
- **Reihenfolge der Specs:** 014 braucht 012, 013, 015 (und damit 011) und 016.
