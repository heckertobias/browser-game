import { describe, expect, it } from "vitest";
import {
  DUMMY_ARM_HEIGHT,
  DUMMY_ARM_WIDTH,
  DUMMY_HEIGHT,
  DUMMY_POST_WIDTH,
  HP_BAR_HEIGHT,
  HP_BAR_WIDTH,
  PLAYER_SIZE,
  PROJECTILE_RADIUS,
  TARGET_OUTLINE_WIDTH,
} from "@/features/game/core/constants";
import { FakeContext } from "./fakeContext";
import { desaturate, hpBarWidth, render, type CombatScene, type DrawablePlayer, type DrawableTarget } from "./render";
import type { Viewport } from "./viewport";

const viewport: Viewport = { scale: 2, canvasWidth: 1280, canvasHeight: 720, viewWidth: 640, viewHeight: 360, offsetX: 0, offsetY: 0 };
const camera = { x: 0, y: 0 };

const grey = (css: string) => desaturate(Number.parseInt(css.slice(1), 16)) & 0xff;

const dummy: DrawableTarget = { id: "dummy", x: 0.4, y: -100.4, hp: 60, maxHp: 100 };
const own: DrawablePlayer = { name: "a", color: 0xffe119, x: 0, y: 0 };
const scene = (partial: Partial<CombatScene>): CombatScene => ({ targets: [], projectiles: [], selectedId: null, ...partial });

function draw(players: readonly DrawablePlayer[], combat: CombatScene): FakeContext {
  const ctx = new FakeContext();
  render(ctx.asContext(), viewport, camera, players, combat);
  return ctx;
}

const posts = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === DUMMY_POST_WIDTH && r.height === DUMMY_HEIGHT);
const arms = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === DUMMY_ARM_WIDTH && r.height === DUMMY_ARM_HEIGHT);
const bars = (ctx: FakeContext) => ctx.rects().filter((r) => r.height === HP_BAR_HEIGHT);
const squares = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === PLAYER_SIZE && r.height === PLAYER_SIZE);

describe("hpBarWidth (AK-18)", () => {
  it.each([
    [100, 100, 20, 20],
    [50, 100, 20, 10],
    [60, 100, 20, 12],
    [0, 100, 20, 0],
    [-5, 100, 20, 0],
    [150, 100, 20, 20],
    [10, 0, 20, 0],
  ])("hp %d von %d bei Breite %d ergibt %d", (hp, maxHp, width, expected) => {
    expect(hpBarWidth(hp, maxHp, width)).toBeCloseTo(expected, 9);
  });

  it("jeder Treffer verkleinert den Balken", () => {
    expect(hpBarWidth(90, 100, HP_BAR_WIDTH)).toBeLessThan(hpBarWidth(100, 100, HP_BAR_WIDTH));
    expect(hpBarWidth(85, 100, HP_BAR_WIDTH)).toBeLessThan(hpBarWidth(90, 100, HP_BAR_WIDTH));
  });
});

describe("render: Ziele (013)", () => {
  it("AK-18: Dummy mit HP-Balken über ihm", () => {
    const ctx = draw([], scene({ targets: [dummy] }));
    const post = posts(ctx);
    expect(post).toHaveLength(1);
    expect(post[0]?.x).toBe(-DUMMY_POST_WIDTH / 2);
    expect(post[0]?.y).toBe(-100 - DUMMY_HEIGHT / 2);
    expect(arms(ctx)).toHaveLength(1);
    const [background, fill] = bars(ctx);
    if (!background || !fill || !post[0]) throw new Error("Balken fehlt");
    expect(bars(ctx)).toHaveLength(2);
    expect(background.width).toBe(HP_BAR_WIDTH);
    expect(fill.width).toBe(hpBarWidth(60, 100, HP_BAR_WIDTH));
    expect([fill.x, fill.y]).toEqual([background.x, background.y]);
    expect(background.y + HP_BAR_HEIGHT).toBeLessThan(post[0].y);
    expect(background.x + HP_BAR_WIDTH / 2).toBe(0);
  });

  it("AK-18: weniger HP, kürzerer Balken", () => {
    const fillWidth = (hp: number) => bars(draw([], scene({ targets: [{ ...dummy, hp }] })))[1]?.width ?? NaN;
    expect(fillWidth(85)).toBeLessThan(fillWidth(100));
  });

  it("AK-1: aktives Ziel bekommt eine Umrandung um den Dummy", () => {
    const ctx = draw([], scene({ targets: [dummy], selectedId: "dummy" }));
    const strokes = ctx.paths("stroke");
    expect(strokes).toHaveLength(1);
    const stroke = strokes[0];
    const background = ctx.rects()[0];
    if (!stroke || !background) throw new Error("Zeichenoperationen fehlen");
    expect(stroke.points).toHaveLength(4);
    const xs = stroke.points.map((p) => p.x);
    const ys = stroke.points.map((p) => p.y);
    for (const rect of [...posts(ctx), ...arms(ctx)]) {
      expect(rect.x).toBeGreaterThan(Math.min(...xs));
      expect(rect.x + rect.width).toBeLessThan(Math.max(...xs));
      expect(rect.y).toBeGreaterThan(Math.min(...ys));
      expect(rect.y + rect.height).toBeLessThan(Math.max(...ys));
    }
    expect(stroke.lineWidth).toBe(TARGET_OUTLINE_WIDTH);
    expect(grey(stroke.style) - grey(background.style)).toBeGreaterThanOrEqual(128);
  });

  it("AK-1, AK-3: ohne Auswahl oder mit unbekannter ID keine Umrandung", () => {
    expect(draw([], scene({ targets: [dummy], selectedId: null })).paths("stroke")).toHaveLength(0);
    expect(draw([], scene({ targets: [dummy], selectedId: "x" })).paths("stroke")).toHaveLength(0);
  });

  it("AK-3: nur das ausgewählte von zwei Zielen wird umrandet", () => {
    const a: DrawableTarget = { ...dummy, id: "a", x: -100 };
    const b: DrawableTarget = { ...dummy, id: "b", x: 100 };
    const strokes = draw([], scene({ targets: [a, b], selectedId: "b" })).paths("stroke");
    expect(strokes).toHaveLength(1);
    const xs = strokes[0]?.points.map((p) => p.x) ?? [];
    expect(Math.min(...xs)).toBeGreaterThan(0);
  });

  it("außerhalb des Sichtfelds nicht gezeichnet", () => {
    const ctx = draw([], scene({ targets: [{ ...dummy, x: 5000 }], selectedId: "dummy" }));
    expect(posts(ctx)).toHaveLength(0);
    expect(arms(ctx)).toHaveLength(0);
    expect(bars(ctx)).toHaveLength(0);
    expect(ctx.paths("stroke")).toHaveLength(0);
  });

  it("ohne Szene wie bisher", () => {
    const ctx = new FakeContext();
    render(ctx.asContext(), viewport, camera, [own]);
    expect(posts(ctx)).toHaveLength(0);
    expect(ctx.arcs()).toHaveLength(0);
  });

  it("Ziele liegen unter den Figuren", () => {
    const ctx = draw([own], scene({ targets: [dummy] }));
    const post = posts(ctx)[0];
    const square = squares(ctx)[0];
    if (!post || !square) throw new Error("Zeichenoperationen fehlen");
    expect(ctx.ops.indexOf(post)).toBeLessThan(ctx.ops.indexOf(square));
  });
});

describe("render: Projektile (013, AK-7)", () => {
  it("ein Kreis je sichtbarem Projektil an seinen Koordinaten", () => {
    const ctx = draw([], scene({ projectiles: [{ x: 10, y: 20 }, { x: -30, y: 5 }] }));
    const arcs = ctx.arcs();
    expect(arcs).toHaveLength(2);
    expect(arcs[0]).toMatchObject({ x: 10, y: 20, radius: PROJECTILE_RADIUS, alpha: 1 });
    expect(arcs[1]).toMatchObject({ x: -30, y: 5, radius: PROJECTILE_RADIUS, alpha: 1 });
  });

  it("außerhalb des Sichtfelds nicht gezeichnet", () => {
    expect(draw([], scene({ projectiles: [{ x: 5000, y: 0 }] })).arcs()).toHaveLength(0);
  });

  it("über den Figuren, auch nach einer wartenden Figur mit voller Deckkraft", () => {
    const ctx = draw([{ ...own, away: true }], scene({ projectiles: [{ x: 1, y: 1 }] }));
    const arc = ctx.arcs()[0];
    const square = squares(ctx)[0];
    if (!arc || !square) throw new Error("Zeichenoperationen fehlen");
    expect(square.alpha).toBeLessThan(1);
    expect(ctx.ops.indexOf(arc)).toBeGreaterThan(ctx.ops.indexOf(square));
    expect(arc.alpha).toBe(1);
  });
});
