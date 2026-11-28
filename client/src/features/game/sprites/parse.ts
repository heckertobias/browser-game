/** Prüfhelfer für die Beschreibungen des Sprite-Formats (ADR 0018); Fehler = "<Fundstelle>: <Erwartung>". */
export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function fail(path: string, expected: string): never {
  throw new Error(`${path}: ${expected}`);
}

export function record(value: unknown, path: string): Record<string, unknown> {
  return isRecord(value) ? value : fail(path, "Objekt erwartet");
}

export function integer(value: unknown, path: string, min: number): number {
  return typeof value === "number" && Number.isInteger(value) && value >= min
    ? value
    : fail(path, `ganze Zahl >= ${String(min)} erwartet`);
}

export function text(value: unknown, path: string): string {
  return typeof value === "string" && value !== "" ? value : fail(path, "nicht leerer Text erwartet");
}
