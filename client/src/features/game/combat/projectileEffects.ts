import type { Vec } from "@/features/game/core/vector";
import type { TargetState } from "@/generated/browsergame/v1/game_pb";

/** Was die Effekte von einem Ziel brauchen; `TargetState` aus dem Snapshot passt direkt. */
export type EffectTarget = Pick<TargetState, "id" | "x" | "y" | "hp" | "maxHp">;

interface Flight {
  from: Vec;
  to: Vec;
  start: number;
  end: number;
}

interface HeldHit {
  targetId: string;
  damage: number;
  releaseAt: number;
}

/**
 * Fernkampf als reine Darstellung (013 AK-21, plan.md Entscheidung 12): Der Server meldet Start und Treffer, der Client
 * fliegt einen eigenen Effekt und zeigt den Schaden erst, wenn der Effekt einschlägt. Ob und wann getroffen wird,
 * entscheidet allein der Server.
 */
export class ProjectileEffects {
  private readonly flights = new Map<number, Flight>();
  private held: HeldHit[] = [];
  private current = new Map<string, EffectTarget>();
  private readonly ghosts = new Map<string, EffectTarget>();

  constructor(private readonly maxMs: number) {}

  /** Neuester Snapshot. Fehlt ein Ziel, dessen Treffer noch zurückgehalten wird, bleibt es bis zum Einschlag sichtbar. */
  observe(targets: readonly EffectTarget[]): void {
    const next = new Map(targets.map((t) => [t.id, { id: t.id, x: t.x, y: t.y, hp: t.hp, maxHp: t.maxHp }]));
    for (const [id, last] of this.current) {
      if (!next.has(id) && this.heldDamage(id) > 0) this.ghosts.set(id, last);
    }
    for (const id of next.keys()) this.ghosts.delete(id);
    this.current = next;
  }

  /** Start: Effekt von `from` nach `to` mit `speed` Welteinheiten/s, höchstens `maxMs` lang. Ohne gültiges Tempo kein Effekt. */
  launch(now: number, projectileId: number, from: Vec, to: Vec, speed: number): void {
    if (!(speed > 0)) return;
    const duration = Math.min((Math.hypot(to.x - from.x, to.y - from.y) / speed) * 1000, this.maxMs);
    this.flights.set(projectileId, { from: { ...from }, to: { ...to }, start: now, end: now + duration });
  }

  /** Treffer: HP im neuesten Stand sinken sofort; fliegt der Effekt noch, wird der Schaden bis zu seinem Einschlag zurückgehalten. */
  hit(now: number, projectileId: number, targetId: string, damage: number): void {
    const target = this.current.get(targetId);
    if (target) target.hp = Math.max(0, target.hp - damage);
    const flight = this.flights.get(projectileId);
    if (flight && flight.end > now) this.held.push({ targetId, damage, releaseAt: flight.end });
  }

  /** Gelandete Effekte entfernen, ihre Treffer freigeben; Ghosts ohne zurückgehaltenen Treffer verschwinden. */
  update(now: number): void {
    for (const [id, flight] of this.flights) {
      if (flight.end <= now) this.flights.delete(id);
    }
    this.held = this.held.filter((h) => h.releaseAt > now);
    for (const id of [...this.ghosts.keys()]) {
      if (this.heldDamage(id) <= 0) this.ghosts.delete(id);
    }
  }

  /** Positionen der fliegenden Effekte zum Zeitpunkt `now` (geradlinig). */
  positions(now: number): Vec[] {
    return [...this.flights.values()].map(({ from, to, start, end }) => {
      const t = end > start ? Math.min(1, Math.max(0, (now - start) / (end - start))) : 1;
      return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t };
    });
  }

  /** Ziele zum Zeichnen: HP plus zurückgehaltene Treffer, höchstens Max-HP; dazu Ghosts. */
  targets(): EffectTarget[] {
    return [...this.current.values(), ...this.ghosts.values()].map((t) => {
      const held = this.heldDamage(t.id);
      return held > 0 ? { ...t, hp: Math.min(t.maxHp, t.hp + held) } : t;
    });
  }

  /** Neue Sitzung (Welcome): alles vergessen. */
  clear(): void {
    this.flights.clear();
    this.held = [];
    this.current = new Map();
    this.ghosts.clear();
  }

  private heldDamage(targetId: string): number {
    return this.held.reduce((sum, h) => (h.targetId === targetId ? sum + h.damage : sum), 0);
  }
}
