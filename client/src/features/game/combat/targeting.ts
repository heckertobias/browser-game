import { create } from "@bufbuild/protobuf";
import type { Vec } from "@/features/game/core/vector";
import { AttackSchema, type Attack, type Attack_Kind, type TargetState } from "@/generated/browsergame/v1/game_pb";

/** Was die Auswahl von einem Ziel braucht; `TargetState` aus dem Snapshot passt direkt. */
export type PickableTarget = Pick<TargetState, "id" | "x" | "y">;

/** Nächstes Ziel höchstens `radius` vom Weltpunkt entfernt (Rand zählt), sonst null. Bei Gleichstand das erste. */
export function pickTarget(world: Vec, targets: readonly PickableTarget[], radius: number): string | null {
  let best: string | null = null;
  let bestDistance = Infinity;
  for (const target of targets) {
    const distance = Math.hypot(target.x - world.x, target.y - world.y);
    if (distance <= radius && distance < bestDistance) {
      best = target.id;
      bestDistance = distance;
    }
  }
  return best;
}

/** Höchstens ein aktives Ziel (AK-1, AK-3). Nur Anzeige; der Server prüft beim Angriff (plan.md Entscheidung 1). */
export class TargetSelection {
  private id: string | null = null;

  get current(): string | null {
    return this.id;
  }

  /** Ersetzt ein vorher gewähltes Ziel (AK-3). */
  select(id: string): void {
    this.id = id;
  }

  /** Hebt die Auswahl auf, wenn ihr Ziel nicht mehr unter `ids` ist (tot, plan.md Entscheidung 2). */
  sync(ids: readonly string[]): void {
    if (this.id !== null && !ids.includes(this.id)) this.id = null;
  }
}

/** Angriff auf das aktive Ziel; ohne Ziel null, dann wird nichts geschickt (AK-2). */
export function attackRequest(selection: TargetSelection, kind: Attack_Kind): Attack | null {
  const targetId = selection.current;
  return targetId === null ? null : create(AttackSchema, { kind, targetId });
}
