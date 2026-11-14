import type { Vec } from "@/features/game/core/vector";

export interface RectOp {
  kind: "rect";
  x: number;
  y: number;
  width: number;
  height: number;
  style: string;
  alpha: number;
}

export interface PathOp {
  kind: "fill" | "stroke";
  points: Vec[];
  style: string;
  alpha: number;
  lineWidth: number;
}

export interface ArcOp {
  kind: "arc";
  x: number;
  y: number;
  radius: number;
  style: string;
  alpha: number;
}

export type DrawOp = RectOp | PathOp | ArcOp;

/** Ersatz für den Canvas-2D-Kontext in Tests: merkt sich Rechtecke und gefüllte/umrandete Pfade. */
export class FakeContext {
  fillStyle = "";
  strokeStyle = "";
  lineWidth = 1;
  lineJoin = "miter";
  globalAlpha = 1;
  font = "";
  textAlign = "";
  textBaseline = "";
  imageSmoothingEnabled = true;
  readonly ops: DrawOp[] = [];
  private path: Vec[] = [];

  asContext(): CanvasRenderingContext2D {
    return this as unknown as CanvasRenderingContext2D;
  }

  rects(): RectOp[] {
    return this.ops.filter((op): op is RectOp => op.kind === "rect");
  }

  paths(kind: PathOp["kind"]): PathOp[] {
    return this.ops.filter((op): op is PathOp => op.kind === kind);
  }

  arcs(): ArcOp[] {
    return this.ops.filter((op): op is ArcOp => op.kind === "arc");
  }

  setTransform(): void {}
  save(): void {}
  restore(): void {}
  rect(): void {}
  clip(): void {}
  fillText(): void {}
  closePath(): void {}

  beginPath(): void {
    this.path = [];
  }

  moveTo(x: number, y: number): void {
    this.path.push({ x, y });
  }

  lineTo(x: number, y: number): void {
    this.path.push({ x, y });
  }

  /** Merkt sich Mittelpunkt und Radius mit der aktuellen Füllfarbe (die Projektile setzen sie vorher). */
  arc(x: number, y: number, radius: number): void {
    this.ops.push({ kind: "arc", x, y, radius, style: this.fillStyle, alpha: this.globalAlpha });
  }

  fillRect(x: number, y: number, width: number, height: number): void {
    this.ops.push({ kind: "rect", x, y, width, height, style: this.fillStyle, alpha: this.globalAlpha });
  }

  fill(): void {
    this.ops.push({ kind: "fill", points: [...this.path], style: this.fillStyle, alpha: this.globalAlpha, lineWidth: this.lineWidth });
  }

  stroke(): void {
    this.ops.push({ kind: "stroke", points: [...this.path], style: this.strokeStyle, alpha: this.globalAlpha, lineWidth: this.lineWidth });
  }
}
