export interface Vec {
  x: number;
  y: number;
}

export const ZERO: Vec = { x: 0, y: 0 };

/** Wie der Server: Länge ≤ 1, ungültige Werte bedeuten Stillstand. */
export function clampDirection(x: number, y: number): Vec {
  if (!Number.isFinite(x) || !Number.isFinite(y)) {
    return ZERO;
  }
  const length = Math.hypot(x, y);
  return length > 1 ? { x: x / length, y: y / length } : { x, y };
}

/**
 * Blickrichtung nach einer Laufrichtung, wie der Server (016 AK-1–3, AK-5): Winkel in Bogenmaß, 0 = +x, π/2 = +y.
 * Stillstand und Richtungen kürzer als `minInput` (aus `Welcome`) lassen `current` stehen.
 */
export function facingOf(dir: Vec, current: number, minInput: number): number {
  const length = Math.hypot(dir.x, dir.y);
  return length > 0 && length >= minInput ? Math.atan2(dir.y, dir.x) : current;
}

const FULL_TURN = 2 * Math.PI;

/** Winkel zwischen `a` und `b` über den kürzeren Bogen (016 AK-7); das Ergebnis ist nicht auf ±π normalisiert. */
export function lerpAngle(a: number, b: number, t: number): number {
  const delta = ((((b - a) % FULL_TURN) + FULL_TURN + Math.PI) % FULL_TURN) - Math.PI;
  return a + delta * t;
}
