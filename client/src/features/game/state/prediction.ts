import { clampDirection, type Vec } from "../engine/vector";

export interface PendingInput {
  seq: number;
  dirX: number;
  dirY: number;
}

/** Unbestätigte Eingaben über diese Zahl hinaus (≈ 5 s) werden verworfen. */
const MAX_PENDING = 100;

/**
 * Client-Prediction für die eigene Figur: Eingaben werden sofort lokal angewandt und
 * beim Snapshot gegen die Server-Position abgeglichen (der Server ist Autorität, AK-5).
 */
export class Predictor {
  /** Position nach dem letzten Fixed-Step. */
  x = 0;
  y = 0;
  /** Position vor dem letzten Fixed-Step, für flüssiges Rendern zwischen zwei Steps. */
  prevX = 0;
  prevY = 0;
  initialized = false;

  private pending: PendingInput[] = [];
  private nextSeq = 1;

  constructor(private readonly stepDistance: number) {}

  /** Ein Fixed-Step: Eingabe lokal anwenden und zum Versenden zurückgeben. */
  step(direction: Vec): PendingInput {
    const dir = clampDirection(direction.x, direction.y);
    const input = { seq: this.nextSeq++, dirX: dir.x, dirY: dir.y };
    this.prevX = this.x;
    this.prevY = this.y;
    this.apply(input);
    this.pending.push(input);
    if (this.pending.length > MAX_PENDING) {
      this.pending.splice(0, this.pending.length - MAX_PENDING);
    }
    return input;
  }

  /** Server-Position übernehmen und noch unbestätigte Eingaben erneut anwenden. */
  reconcile(serverX: number, serverY: number, lastProcessedSeq: number): void {
    const before = { x: this.x, y: this.y };
    this.pending = this.pending.filter((input) => input.seq > lastProcessedSeq);
    this.x = serverX;
    this.y = serverY;
    for (const input of this.pending) {
      this.apply(input);
    }

    if (this.initialized) {
      // Korrektur auch auf die Vorposition anwenden, damit die Interpolation nicht zurückspringt.
      this.prevX += this.x - before.x;
      this.prevY += this.y - before.y;
    } else {
      this.prevX = this.x;
      this.prevY = this.y;
      this.initialized = true;
    }
  }

  /** Darstellungsposition zwischen zwei Fixed-Steps (alpha ∈ [0, 1]). */
  renderPosition(alpha: number): Vec {
    return { x: this.prevX + (this.x - this.prevX) * alpha, y: this.prevY + (this.y - this.prevY) * alpha };
  }

  get pendingCount(): number {
    return this.pending.length;
  }

  private apply(input: PendingInput): void {
    this.x += input.dirX * this.stepDistance;
    this.y += input.dirY * this.stepDistance;
  }
}
