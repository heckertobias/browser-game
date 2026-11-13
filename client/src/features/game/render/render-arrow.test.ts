import { describe, expect, it } from "vitest";
import { PLAYER_SIZE } from "@/features/game/core/constants";
import type { Vec } from "@/features/game/core/vector";
import { FakeContext } from "./fakeContext";
import { arrowPoints, desaturate, render, type DrawablePlayer } from "./render";
import type { Viewport } from "./viewport";

const viewport: Viewport = { scale: 2, canvasWidth: 1280, canvasHeight: 720, viewWidth: 640, viewHeight: 360, offsetX: 0, offsetY: 0 };
const camera = { x: 0, y: 0 };

const deg = (d: number) => (d * Math.PI) / 180;

function expectPoints(actual: readonly Vec[], expected: readonly Vec[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((point, i) => {
    expect(actual[i]?.x).toBeCloseTo(point.x, 9);
    expect(actual[i]?.y).toBeCloseTo(point.y, 9);
  });
}

function draw(players: readonly DrawablePlayer[]): FakeContext {
  const ctx = new FakeContext();
  render(ctx.asContext(), viewport, camera, players);
  return ctx;
}

/** Grauwert 0–255 einer CSS-Farbe `#rrggbb` (gleiche Gewichtung wie `desaturate`). */
const grey = (css: string) => desaturate(Number.parseInt(css.slice(1), 16)) & 0xff;
const squares = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === PLAYER_SIZE && r.height === PLAYER_SIZE);

const own: DrawablePlayer = { name: "a", color: 0xffe119, x: 0, y: 0, facing: 0 };
const other: DrawablePlayer = { name: "b", color: 0x4363d8, x: 40.4, y: 19.6, facing: deg(37) };
const away: DrawablePlayer = { name: "c", color: 0xe6194b, x: -40, y: -20, facing: Math.PI, away: true };

describe("render: Pfeil an jeder Figur (016)", () => {
  it("AK-6, AK-8: jede sichtbare Figur bekommt einen Pfeil in ihre Blickrichtung", () => {
    const fills = draw([own, other, away]).paths("fill");
    expect(fills).toHaveLength(3);
    expectPoints(fills[0]?.points ?? [], arrowPoints(0, 0, 0));
    expectPoints(fills[1]?.points ?? [], arrowPoints(40, 20, deg(37)));
    expectPoints(fills[2]?.points ?? [], arrowPoints(-40, -20, Math.PI));
  });

  it("AK-7: Pfeil sitzt am gezeichneten Quadrat, der Winkel bleibt ungerundet", () => {
    const ctx = draw([other]);
    const square = squares(ctx)[0];
    expectPoints(
      ctx.paths("fill")[0]?.points ?? [],
      arrowPoints((square?.x ?? 0) + PLAYER_SIZE / 2, (square?.y ?? 0) + PLAYER_SIZE / 2, deg(37)),
    );
  });

  it("AK-6: dunkler Rand unter heller Füllung, hell gegen den Hintergrund", () => {
    const ctx = draw([own]);
    const stroke = ctx.paths("stroke")[0];
    const fill = ctx.paths("fill")[0];
    const background = ctx.rects()[0];
    if (!stroke || !fill || !background) throw new Error("Zeichenoperationen fehlen");
    expect(ctx.ops.indexOf(stroke)).toBeLessThan(ctx.ops.indexOf(fill));
    expectPoints(stroke.points, fill.points);
    expect(stroke.lineWidth).toBeGreaterThan(0);
    expect(grey(fill.style) - grey(stroke.style)).toBeGreaterThanOrEqual(128);
    expect(grey(fill.style) - grey(background.style)).toBeGreaterThanOrEqual(128);
  });

  it("AK-6: wartende Figur zeigt den Pfeil mit derselben Transparenz wie die Figur", () => {
    const ctx = draw([own, away]);
    const awaySquare = squares(ctx)[1];
    const [ownFill, awayFill] = ctx.paths("fill");
    expect(awayFill?.alpha).toBe(awaySquare?.alpha);
    expect(awayFill?.alpha).toBeLessThan(1);
    expect(ownFill?.alpha).toBe(1);
  });

  it("ohne Blickrichtung oder außerhalb des Sichtfelds kein Pfeil", () => {
    const ctx = draw([
      { name: "d", color: 0, x: 0, y: 0 },
      { name: "e", color: 0, x: 5000, y: 0, facing: 0 },
    ]);
    expect(ctx.paths("fill")).toHaveLength(0);
    expect(ctx.paths("stroke")).toHaveLength(0);
  });
});
