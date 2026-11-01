export interface RemotePlayer {
  id: string;
  name: string;
  color: number;
  x: number;
  y: number;
  away?: boolean;
}

interface TimedSnapshot {
  time: number;
  players: Map<string, RemotePlayer>;
}

/** Wie lange Snapshots aufbewahrt werden (ms). */
const RETENTION_MS = 1000;

/**
 * Puffer für die Darstellung anderer Spieler: Gerendert wird leicht in der Vergangenheit,
 * zwischen den zwei Snapshots, die diesen Zeitpunkt einschließen.
 */
export class SnapshotBuffer {
  private snapshots: TimedSnapshot[] = [];

  push(time: number, players: Iterable<RemotePlayer>): void {
    this.snapshots.push({ time, players: new Map([...players].map((p) => [p.id, p])) });
    const cutoff = time - RETENTION_MS;
    while (this.snapshots.length > 2 && (this.snapshots[1]?.time ?? Infinity) < cutoff) {
      this.snapshots.shift();
    }
  }

  /** Spieler zum Zeitpunkt `renderTime`. Wer im späteren Snapshot fehlt, ist weg (AK-4). */
  sample(renderTime: number): RemotePlayer[] {
    const later = this.snapshots.findIndex((s) => s.time >= renderTime);
    const newest = this.snapshots.at(-1);
    if (!newest) {
      return [];
    }
    if (later === -1) {
      return [...newest.players.values()];
    }

    const to = this.snapshots[later];
    const from = this.snapshots[later - 1];
    if (!to) {
      return [];
    }
    if (!from) {
      return [...to.players.values()];
    }

    const span = to.time - from.time;
    const t = span > 0 ? (renderTime - from.time) / span : 1;
    return [...to.players.values()].map((target) => {
      const start = from.players.get(target.id);
      return start
        ? { ...target, x: start.x + (target.x - start.x) * t, y: start.y + (target.y - start.y) * t }
        : target;
    });
  }
}
