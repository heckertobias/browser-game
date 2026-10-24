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
