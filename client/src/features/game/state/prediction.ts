import { clampDirection, facingOf, type Vec } from "@/features/game/core/vector";

/** Eine Eingabe je festem Schritt; der Server bestätigt sie über `lastProcessedSeq` im Snapshot. */
export interface PendingInput {
  seq: number;
  dirX: number;
  dirY: number;
}

/** Unbestätigte Eingaben über diese Zahl hinaus (≈ 5 s) werden verworfen. */
const MAX_PENDING = 100;

/**
 * Client-Prediction für die eigene Figur: Eingaben werden sofort lokal angewandt und
 * beim Snapshot gegen die Server-Position abgeglichen (der Server ist Autorität, 001 AK-5).
 */
export class Predictor {
  /** Position nach dem letzten Fixed-Step. */
  x = 0;
  y = 0;
  /** Position vor dem letzten Fixed-Step, für flüssiges Rendern zwischen zwei Steps. */
  prevX = 0;
  prevY = 0;
  /** Blickrichtung in Bogenmaß (016); gilt erst nach dem ersten `reconcile` (Server-Wert, AK-4). */
  facing = 0;
  /** Erst nach dem ersten Snapshot mit eigener Figur gibt es eine Position; vorher wird nichts vorhergesagt. */
  initialized = false;

  private pending: PendingInput[] = [];
  private didMove = false;
  private nextSeq = 1;

  constructor(
    private readonly stepDistance: number,
    /** Aus `Welcome.minFacingInput` (Constitution §8). */
    private readonly minFacingInput = 0,
  ) {}

  /** Ein Fixed-Step: Eingabe lokal anwenden und zum Versenden zurückgeben. */
  step(direction: Vec): PendingInput {
    const dir = clampDirection(direction.x, direction.y);
    const input = { seq: this.nextSeq++, dirX: dir.x, dirY: dir.y };
    this.prevX = this.x;
    this.prevY = this.y;
    this.apply(input);
    this.didMove = this.x !== this.prevX || this.y !== this.prevY;
    this.pending.push(input);
    if (this.pending.length > MAX_PENDING) {
      this.pending.splice(0, this.pending.length - MAX_PENDING);
    }
    return input;
  }

  /** Server-Position und -Blickrichtung übernehmen und noch unbestätigte Eingaben erneut anwenden. */
  reconcile(serverX: number, serverY: number, lastProcessedSeq: number, serverFacing?: number): void {
    const before = { x: this.x, y: this.y };
    this.pending = this.pending.filter((input) => input.seq > lastProcessedSeq);
    this.x = serverX;
    this.y = serverY;
    this.facing = serverFacing ?? this.facing;
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

  /** Die eigene Figur läuft, wenn der letzte Fixed-Step ihre Position verändert hat (017 AK-7, AK-8). */
  get moving(): boolean {
    return this.didMove;
  }

  /** Zahl der noch unbestätigten Eingaben. */
  get pendingCount(): number {
    return this.pending.length;
  }

  private apply(input: PendingInput): void {
    this.x += input.dirX * this.stepDistance;
    this.y += input.dirY * this.stepDistance;
    this.facing = facingOf({ x: input.dirX, y: input.dirY }, this.facing, this.minFacingInput);
  }
}
