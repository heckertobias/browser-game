import { describe, expect, it } from "vitest";
import { DUMMY_POST_WIDTH, PLAYER_SIZE, START_MARKER_COLOR, START_MARKER_SIZE } from "@/features/game/core/constants";
import { group, parseAtlas, type Atlas } from "@/features/game/sprites/atlas";
import { GROUND_ENTRIES, groundTile } from "@/features/game/world/groundTiles";
import { testMap } from "@/features/game/world/testMap";
import { FakeContext, type ImageOp } from "./fakeContext";
import { cameraOrigin, render, type DrawablePlayer, type DrawableTarget, type GroundLayer, type RenderOptions } from "./render";
import { computeViewport, type Viewport } from "./viewport";

/** Atlas mit eindeutigem Rechteck je Name, 8 Spalten zu 16 Pixeln. */
function testAtlas(names: readonly string[]): Atlas {
  return parseAtlas({
    image: "test.png",
    entries: Object.fromEntries(
      names.map((name, i) => [name, { x: (i % 8) * 16, y: Math.floor(i / 8) * 16, width: 16, height: 16 }]),
    ),
  });
}

const NAMES = [...GROUND_ENTRIES, "grass.1", "grass.2"];

const MAP = testMap(
  [
    "^^^^^^^^^^^^",
    "^^^^^^^^^^^^",
    "^^........^^",
    "^^.==.~~..^^",
    "^^.==.~~..^^",
    "^^........^^",
    "^^^^^^^^^^^^",
    "^^^^^^^^^^^^",
  ],
  -6,
  -4,
);

const atlas = testAtlas(NAMES);
const image = { id: "ground" } as unknown as CanvasImageSource;
const layer = (map = MAP): GroundLayer => ({ map, atlas, image, variants: group(atlas, "grass") });

class TransformContext extends FakeContext {
  matrix: readonly number[] = [1, 0, 0, 1, 0, 0];
  override setTransform(a = 1, b = 0, c = 0, d = 1, e = 0, f = 0): void {
    this.matrix = [a, b, c, d, e, f];
  }
}

class StyledContext extends TransformContext {
  readonly textStyles: string[] = [];
  override fillText(text: string, x: number, y: number): void {
    this.textStyles.push(this.fillStyle);
    super.fillText(text, x, y);
  }
}

function draw<C extends FakeContext = TransformContext>(
  viewport: Viewport,
  camera: { x: number; y: number },
  options: RenderOptions,
  players: readonly DrawablePlayer[] = [],
  targets: readonly DrawableTarget[] = [],
  ctx = new TransformContext() as unknown as C,
): C {
  render(ctx.asContext(), viewport, camera, players, { targets, projectiles: [], selectedId: null }, options);
  return ctx;
}

const groundImages = (ctx: FakeContext) => ctx.images().filter((op) => op.source === image);
const gridLines = (ctx: FakeContext) => ctx.rects().filter((r) => r.width <= 1 || r.height <= 1);
const markerRects = (ctx: FakeContext) => ctx.rects().filter((r) => r.style === START_MARKER_COLOR);

function nameOf(op: ImageOp): string | undefined {
  return [...atlas.entries].find(([, e]) => e.x === op.sx && e.y === op.sy)?.[0];
}

function expectCovered(ctx: TransformContext, viewport: Viewport, camera: { x: number; y: number }): void {
  const ops = groundImages(ctx);
  expect(ops.length).toBeGreaterThan(0);
  const origin = cameraOrigin(camera, viewport);
  expect(Number.isInteger(viewport.offsetX)).toBe(true);
  expect(Number.isInteger(viewport.offsetY)).toBe(true);
  const near = (v: number) => Math.abs(v - Math.round(v)) < 1e-6;
  for (const op of ops) {
    expect(Number.isInteger(op.sx) && Number.isInteger(op.sy)).toBe(true);
    expect([op.sw, op.sh, op.dw, op.dh]).toEqual([16, 16, 16, 16]);
    expect(op.dx % 16 === 0 && op.dy % 16 === 0).toBe(true);
    const [a, , , d, e, f] = ctx.matrix as [number, number, number, number, number, number];
    for (const v of [a * op.dx + e, a * (op.dx + op.dw) + e, d * op.dy + f, d * (op.dy + op.dh) + f]) {
      expect(near(v)).toBe(true);
    }
  }
  const xs = [...new Set(ops.map((op) => op.dx))].sort((a, b) => a - b);
  const ys = [...new Set(ops.map((op) => op.dy))].sort((a, b) => a - b);
  for (const axis of [xs, ys]) {
    axis.slice(1).forEach((v, i) => {
      expect(v - (axis[i] ?? 0)).toBe(16);
    });
  }
  expect(ops).toHaveLength(xs.length * ys.length);
  expect(new Set(ops.map((op) => `${String(op.dx)},${String(op.dy)}`)).size).toBe(ops.length);
  expect(xs[0] ?? Infinity).toBeLessThanOrEqual(origin.x);
  expect((xs.at(-1) ?? -Infinity) + 16).toBeGreaterThanOrEqual(origin.x + viewport.viewWidth);
  expect(ys[0] ?? Infinity).toBeLessThanOrEqual(origin.y);
  expect((ys.at(-1) ?? -Infinity) + 16).toBeGreaterThanOrEqual(origin.y + viewport.viewHeight);
}

describe("render: Boden (018)", () => {
  const view = computeViewport(1280, 720, 1);
  const origin = { x: 0, y: 0 };

  it("AK-1: mit Boden je Kachel das Bodenstück aus dem Atlas, kein Gitter", () => {
    const ctx = draw(view, origin, { ground: layer() });
    const ops = groundImages(ctx);
    const variants = group(atlas, "grass");
    const names = new Set<string | undefined>();
    for (const op of ops) {
      const expected = atlas.entries.get(groundTile(MAP, op.dx / 16, op.dy / 16, variants));
      expect({ x: op.sx, y: op.sy }).toEqual({ x: expected?.x, y: expected?.y });
      names.add(nameOf(op));
    }
    expect(names.has("mountain")).toBe(true);
    expect([...names].some((n) => n?.startsWith("path."))).toBe(true);
    expect([...names].some((n) => n?.startsWith("water."))).toBe(true);
    expect([...names].some((n) => n === "grass" || n?.startsWith("grass."))).toBe(true);
    expect(gridLines(ctx)).toHaveLength(0);
    expect(ctx.imageSmoothingEnabled).toBe(false);
  });

  it.each([{}, { ground: null }])("Entscheidung 9: ohne Boden das Gitter, keine Kacheln (%j)", (options) => {
    const ctx = draw(view, origin, options);
    expect(ctx.images()).toHaveLength(0);
    expect(gridLines(ctx).length).toBeGreaterThan(0);
  });

  it.each([
    [1280, 720, 1],
    [1536, 730, 1.25],
    [412, 915, 2.625],
    [3440, 1440, 1],
    [2000, 800, 1],
  ])("AK-2: Sichtfeld %i x %i (dpr %f) lückenlos auf Gerätepixeln", (w, h, dpr) => {
    const viewport = computeViewport(w, h, dpr);
    for (const camera of [{ x: 0, y: 0 }, { x: 13.37, y: -4.21 }, { x: -1000.5, y: 777.7 }]) {
      const ctx = draw(viewport, camera, { ground: layer() });
      expectCovered(ctx, viewport, camera);
      expect(groundImages(ctx).length).toBeLessThanOrEqual(60 * 27);
    }
  });

  it("AK-5: Sichtfeld halb über dem Kartenrand lückenlos, außen Berg", () => {
    const camera = { x: 96, y: 0 };
    const ctx = draw(view, camera, { ground: layer() });
    expectCovered(ctx, view, camera);
    const ops = groundImages(ctx);
    expect(ops.filter((op) => op.dx >= 96).every((op) => nameOf(op) === "mountain")).toBe(true);
    expect(ops.some((op) => op.dx < 96 && nameOf(op) !== "mountain")).toBe(true);
  });

  it("AK-5: weit außerhalb der Karte nur Berg, nie leere Fläche", () => {
    const camera = { x: -5000, y: -5000 };
    const ctx = draw(view, camera, { ground: layer() });
    expectCovered(ctx, view, camera);
    expect(groundImages(ctx).every((op) => nameOf(op) === "mountain")).toBe(true);
  });

  it("AK-5: endet die Karte in Wasser, geht das Wasser weiter", () => {
    const map = testMap(["......", "..~~~~", "..~~~~", "......"]);
    const camera = { x: 400, y: 24 };
    const ctx = draw(view, camera, { ground: layer(map) });
    expectCovered(ctx, view, camera);
    const water = groundImages(ctx).filter((op) => op.dx >= 96 && (op.dy === 16 || op.dy === 32));
    expect(water.length).toBeGreaterThan(0);
    expect(water.every((op) => nameOf(op)?.startsWith("water.") === true)).toBe(true);
  });

  const players: DrawablePlayer[] = [{ name: "p", color: 0x4363d8, x: 40, y: 20 }];
  const targets: DrawableTarget[] = [{ id: "d", x: 0, y: -100, hp: 1, maxHp: 1 }];

  it("AK-7: Kreuz und Beschriftung in Magenta an (0, 0), über dem Boden, unter Ziel und Figur", () => {
    const ctx = draw(view, origin, { ground: layer(), startMarker: "marker-label" }, players, targets, new StyledContext());
    const styled = ctx;
    const marks = markerRects(ctx);
    expect(marks).toHaveLength(2);
    const horizontal = marks.find((r) => r.width === START_MARKER_SIZE);
    const vertical = marks.find((r) => r.height === START_MARKER_SIZE);
    expect(horizontal?.x).toBe(-START_MARKER_SIZE / 2);
    expect((horizontal?.y ?? NaN) + (horizontal?.height ?? NaN) / 2).toBe(0);
    expect(vertical?.y).toBe(-START_MARKER_SIZE / 2);
    expect((vertical?.x ?? NaN) + (vertical?.width ?? NaN) / 2).toBe(0);

    const textIndex = ctx.ops.findIndex((op) => op.kind === "text" && op.text === "marker-label");
    const text = ctx.ops[textIndex];
    expect(text?.kind === "text" && text.x === 0 && text.y > 0).toBe(true);
    expect(styled.textStyles[ctx.texts().findIndex((op) => op.text === "marker-label")]).toBe(START_MARKER_COLOR);

    const lastGround = ctx.ops.findLastIndex((op) => op.kind === "image");
    const cross = marks.map((r) => ctx.ops.indexOf(r));
    const target = ctx.ops.findIndex((op, i) => i > textIndex && op.kind === "rect" && op.width === DUMMY_POST_WIDTH);
    const player = ctx.ops.findIndex((op) => op.kind === "rect" && op.width === PLAYER_SIZE && op.height === PLAYER_SIZE);
    expect(lastGround).toBeLessThan(Math.min(...cross));
    expect(Math.max(...cross)).toBeLessThan(textIndex);
    expect(textIndex).toBeLessThan(target);
    expect(target).toBeLessThan(player);
  });

  it("AK-7: Markierung auch über dem Gitter", () => {
    const ctx = draw(view, origin, { startMarker: "marker-label" });
    const marks = markerRects(ctx);
    expect(marks).toHaveLength(2);
    expect(ctx.texts().some((op) => op.text === "marker-label")).toBe(true);
    expect(gridLines(ctx).length).toBeGreaterThan(0);
    const lastGrid = ctx.ops.indexOf(gridLines(ctx).at(-1) as never);
    expect(ctx.ops.indexOf(marks[0] as never)).toBeGreaterThan(lastGrid);
  });

  it("ohne startMarker keine Markierung", () => {
    const ctx = draw(view, origin, { ground: layer() });
    expect(markerRects(ctx)).toHaveLength(0);
    expect(ctx.texts()).toHaveLength(0);
  });
});
