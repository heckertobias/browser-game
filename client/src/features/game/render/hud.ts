/** Eigene Koordinaten fürs HUD, ganze Zahlen (AK-19b). */
export function formatCoordinates(x: number, y: number): string {
  return `X ${String(Math.round(x))} · Y ${String(Math.round(y))}`;
}
