import { lerpAngle } from "@/features/game/core/vector";

/** Andere Figur aus dem Snapshot, so wie sie gezeichnet wird. */
export interface RemotePlayer {
  id: string;
  name: string;
  color: number;
  x: number;
  y: number;
  /** Verbindung verloren, Figur wartet noch (003 AK-20). */
  away?: boolean;
  /** Zählt Sprünge der Figur (Respawn); ändert er sich, wird nicht interpoliert (012 AK-8). */
  spawnSeq?: number;
  /** Blickrichtung in Bogenmaß (016); interpoliert über den kürzeren Bogen. */
  facing?: number;
}

interface TimedSnapshot {
  time: number;
  players: Map<string, RemotePlayer>;
}

/** Wie lange Snapshots aufbewahrt werden (ms). */
const RETENTION_MS = 1000;

/**
 * Puffer für die Darstellung anderer Spieler (001 AK-3): Gerendert wird leicht in der Vergangenheit,
 * zwischen den zwei Snapshots, die diesen Zeitpunkt einschließen. So bewegen sich andere Figuren flüssig,
 * obwohl Snapshots nur im Server-Takt kommen.
 */
export class SnapshotBuffer {
  private snapshots: TimedSnapshot[] = [];

  /** Neuer Snapshot zur Empfangszeit `time`; ältere als `RETENTION_MS` fallen weg, soweit sie keiner Interpolation mehr dienen. */
  push(time: number, players: Iterable<RemotePlayer>): void {
    this.snapshots.push({ time, players: new Map([...players].map((player) => [player.id, player])) });
    const cutoff = time - RETENTION_MS;
    while (this.snapshots.length > 2 && (this.snapshots[1]?.time ?? Infinity) < cutoff) {
      this.snapshots.shift();
    }
  }

  /**
   * Spieler zum Zeitpunkt `renderTime`. Wer im späteren Snapshot fehlt, ist weg (001 AK-4). Wer gesprungen ist (anderer
   * `spawnSeq`), steht sofort an der neuen Position (012 AK-8). Die Blickrichtung wird über den kürzeren Bogen
   * interpoliert (016 AK-7).
   */
  sample(renderTime: number): RemotePlayer[] {
    const newest = this.snapshots.at(-1);
    if (!newest) {
      return [];
    }
    const pair = this.around(renderTime);
    if (!pair) {
      return [...newest.players.values()];
    }
    const { from, to } = pair;
    if (!from) {
      return [...to.players.values()];
    }

    const span = to.time - from.time;
    const t = span > 0 ? (renderTime - from.time) / span : 1;
    return [...to.players.values()].map((target): RemotePlayer => {
      const start = from.players.get(target.id);
      if (!start || start.spawnSeq !== target.spawnSeq) {
        return target;
      }
      const facing =
        start.facing !== undefined && target.facing !== undefined
          ? { facing: lerpAngle(start.facing, target.facing, t) }
          : {};
      return {
        ...target,
        x: start.x + (target.x - start.x) * t,
        y: start.y + (target.y - start.y) * t,
        ...facing,
      };
    });
  }

  /**
   * IDs der Figuren, die sich zum Zeitpunkt `renderTime` sichtbar bewegen (017 AK-7, AK-8): in beiden Snapshots um
   * `renderTime` vorhanden, gleicher `spawnSeq`, andere Position. Außerhalb der Snapshots: leer.
   */
  moving(renderTime: number): ReadonlySet<string> {
    const ids = new Set<string>();
    const pair = this.around(renderTime);
    const from = pair?.from;
    if (!pair || !from) {
      return ids;
    }
    for (const target of pair.to.players.values()) {
      const start = from.players.get(target.id);
      if (start && start.spawnSeq === target.spawnSeq && (start.x !== target.x || start.y !== target.y)) {
        ids.add(target.id);
      }
    }
    return ids;
  }

  /** Die beiden Snapshots, zwischen denen `renderTime` liegt; `from` fehlt vor dem ältesten, alles fehlt nach dem neuesten. */
  private around(renderTime: number): { from: TimedSnapshot | undefined; to: TimedSnapshot } | undefined {
    const later = this.snapshots.findIndex((s) => s.time >= renderTime);
    const to = this.snapshots[later];
    if (later === -1 || !to) {
      return undefined;
    }
    return { from: this.snapshots[later - 1], to };
  }
}
