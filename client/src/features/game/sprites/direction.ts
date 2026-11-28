import type { SpriteDirection } from "./rig";

/** Toleranz für „genau schräg“ (017 AK-5): Die Blickrichtung kommt als float32; bei 45° sind cos und sin danach nicht mehr gleich. */
export const FACING_TIE_EPSILON = 1e-4;

/** Nächstliegende der vier Richtungen (017 Entscheidung 4); genau schräg gewinnt die Seite. y wächst nach unten. */
export function spriteDirection(facing: number): SpriteDirection {
  const c = Math.cos(facing);
  const s = Math.sin(facing);
  if (Math.abs(c) >= Math.abs(s) - FACING_TIE_EPSILON) return c >= 0 ? "right" : "left";
  return s > 0 ? "down" : "up";
}
