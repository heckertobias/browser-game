/** Eigene Koordinaten fürs HUD, ganze Zahlen (003 AK-19b). */
export function formatCoordinates(x: number, y: number): string {
  return `X ${String(Math.round(x))} · Y ${String(Math.round(y))}`;
}
