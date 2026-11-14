import { lerpAngle } from "@/features/game/core/vector";

export interface RemotePlayer {
  id: string;
  name: string;
  color: number;
  x: number;
  y: number;
  away?: boolean;
  /** Zählt Sprünge der Figur (Respawn); ändert er sich, wird nicht interpoliert (012 AK-8). */
  spawnSeq?: number;
  /** Blickrichtung in Bogenmaß (016); interpoliert über den kürzeren Bogen. */
  facing?: number;
}

/** Was der Puffer interpoliert: Spieler (001) und Projektile (013). */
export interface Interpolatable {
  id: string | number;
  x: number;
  y: number;
  spawnSeq?: number;
  facing?: number;
}

interface TimedSnapshot<T extends Interpolatable> {
  time: number;
  items: Map<T["id"], T>;
}

/** Wie lange Snapshots aufbewahrt werden (ms). */
const RETENTION_MS = 1000;

/**
 * Puffer für die Darstellung anderer Spieler und der Projektile (013): Gerendert wird leicht in der Vergangenheit,
 * zwischen den zwei Snapshots, die diesen Zeitpunkt einschließen.
 */
export class SnapshotBuffer<T extends Interpolatable = RemotePlayer> {
  private snapshots: TimedSnapshot<T>[] = [];

  push(time: number, items: Iterable<T>): void {
    this.snapshots.push({ time, items: new Map([...items].map((item) => [item.id, item])) });
    const cutoff = time - RETENTION_MS;
    while (this.snapshots.length > 2 && (this.snapshots[1]?.time ?? Infinity) < cutoff) {
      this.snapshots.shift();
    }
  }

  /** Spieler zum Zeitpunkt `renderTime`. Wer im späteren Snapshot fehlt, ist weg (AK-4). Wer gesprungen ist (anderer `spawnSeq`), steht sofort an der neuen Position (012 AK-8). Die Blickrichtung wird über den kürzeren Bogen interpoliert (016 AK-7). */
  sample(renderTime: number): T[] {
    const later = this.snapshots.findIndex((s) => s.time >= renderTime);
    const newest = this.snapshots.at(-1);
    if (!newest) {
      return [];
    }
    if (later === -1) {
      return [...newest.items.values()];
    }

    const to = this.snapshots[later];
    const from = this.snapshots[later - 1];
    if (!to) {
      return [];
    }
    if (!from) {
      return [...to.items.values()];
    }

    const span = to.time - from.time;
    const t = span > 0 ? (renderTime - from.time) / span : 1;
    return [...to.items.values()].map((target): T => {
      const start = from.items.get(target.id);
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
}
