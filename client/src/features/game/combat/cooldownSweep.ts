/** Farbe der abgedunkelten Fläche (013 AK-22). Reine Darstellung, kein Spielwert (Constitution §8). */
const SWEEP_COLOR = "rgb(0 0 0 / 0.6)";

const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));

/** Noch laufender Anteil der Abklingzeit: 1 direkt beim Start, 0 am Ende (013 AK-22). Ohne gültige Dauer 0. */
export function cooldownRemaining(now: number, startedAt: number, durationMs: number): number {
  if (!(durationMs > 0)) return 0;
  return clamp01(1 - (now - startedAt) / durationMs);
}

/** Um so viel Grad ist die dunkle Fläche schon abgelaufen, im Uhrzeigersinn ab 12 Uhr: 0 beim Start, 360 am Ende. */
export function sweepAngle(remaining: number): number {
  return (1 - clamp01(remaining)) * 360;
}

/** Hintergrund des Sweeps: frei bis zum abgelaufenen Winkel, danach dunkel. `conic-gradient` beginnt oben und läuft im Uhrzeigersinn. */
export function sweepBackground(remaining: number): string {
  const angle = sweepAngle(remaining).toFixed(1);
  return `conic-gradient(transparent ${angle}deg, ${SWEEP_COLOR} ${angle}deg)`;
}

/**
 * Taktet einen Sweep über `durationMs` und meldet den Rest (1 … 0) pro Frame (013 AK-22). Ein neuer Start setzt ihn zurück.
 * Uhr und Frames sind für Tests ersetzbar.
 */
export class CooldownSweep {
  private startedAt = 0;
  private durationMs = 0;
  private handle: number | undefined;

  constructor(
    private readonly onChange: (remaining: number) => void,
    private readonly now: () => number = () => performance.now(),
    private readonly requestFrame: (callback: (time: number) => void) => number = (callback) => requestAnimationFrame(callback),
    private readonly cancelFrame: (handle: number) => void = (handle) => { cancelAnimationFrame(handle); },
  ) {}

  /** Startet den Sweep neu; eine ungültige Dauer (≤ 0, NaN) ändert nichts. */
  start(durationMs: number): void {
    if (!(durationMs > 0)) return;
    this.clear();
    this.startedAt = this.now();
    this.durationMs = durationMs;
    this.onChange(1);
    this.handle = this.requestFrame(this.frame);
  }

  /** Beim Abbau: danach kommt kein Rückruf mehr. */
  clear(): void {
    if (this.handle !== undefined) this.cancelFrame(this.handle);
    this.handle = undefined;
  }

  private readonly frame = (time: number): void => {
    const remaining = cooldownRemaining(time, this.startedAt, this.durationMs);
    this.onChange(remaining);
    this.handle = remaining > 0 ? this.requestFrame(this.frame) : undefined;
  };
}
