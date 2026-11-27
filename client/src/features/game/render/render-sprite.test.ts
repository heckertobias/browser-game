import { describe, expect, it, vi } from "vitest";
import { PLAYER_SIZE } from "@/features/game/core/constants";
import type { Pixels } from "@/features/game/sprites/compose";
import { parseRig, sheetSize, type SpriteDirection } from "@/features/game/sprites/rig";
import { playerAppearance, SpriteSet } from "@/features/game/sprites/spriteSet";
import { blank } from "@/features/game/sprites/testPixels";
import { FakeContext } from "./fakeContext";
import { desaturate, render, toCssColor, type DrawablePlayer, type RenderOptions } from "./render";
import type { Viewport } from "./viewport";

const viewport: Viewport = { scale: 2, canvasWidth: 1280, canvasHeight: 720, viewWidth: 640, viewHeight: 360, offsetX: 0, offsetY: 0 };
const camera = { x: 3.3, y: -7.7 };

const rig = parseRig({
  cell: { width: 48, height: 40 },
  origin: { x: 24, y: 20 },
  directions: ["down", "up", "left", "right"],
  animations: { idle: { firstRow: 0, frames: 1 }, walk: { firstRow: 1, frames: 4, frameMs: 100 } },
  slots: ["body", "clothing"],
  parts: {
    "body/default": { slot: "body", image: "body.png" },
    "clothing/ninja": { slot: "clothing", image: "clothing.png", tint: true },
  },
});

function spriteSet(): SpriteSet {
  const size = sheetSize(rig);
  const parts = new Map<string, Pixels>([
    ["body/default", blank(size.width, size.height)],
    ["clothing/ninja", blank(size.width, size.height)],
  ]);
  const toSource = vi.fn((pixels: Pixels) => ({ pixels }) as unknown as CanvasImageSource);
  return new SpriteSet(rig, parts, toSource);
}

function draw(players: readonly DrawablePlayer[], options?: RenderOptions): FakeContext {
  const ctx = new FakeContext();
  render(ctx.asContext(), viewport, camera, players, undefined, options);
  return ctx;
}

const squares = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === PLAYER_SIZE && r.height === PLAYER_SIZE);
const column = (d: SpriteDirection) => rig.directions.indexOf(d) * rig.cell.width;

const own: DrawablePlayer = { name: "a", color: 0xffe119, x: 0, y: 0, facing: 0 };
const other: DrawablePlayer = { name: "b", color: 0x4363d8, x: 40.4, y: 19.6, facing: Math.PI / 2 };
const away: DrawablePlayer = { name: "c", color: 0xe6194b, x: -40.3, y: -20.6, facing: Math.PI, away: true };
const three = [own, other, away];

describe("render: Figur als Sprite (017)", () => {
  it("AK-1, AK-14: mit Sprites je sichtbarer Figur ein Bild, kein Quadrat", () => {
    const sprites = spriteSet();
    const ctx = draw([own, other, away, { ...own, name: "d", x: 5000 }], { sprites });
    const images = ctx.images();
    expect(images).toHaveLength(3);
    expect(squares(ctx)).toHaveLength(0);
    expect(images[0]?.source).toBe(sprites.sheet(playerAppearance(own.color), false));
    expect(images[1]?.source).toBe(sprites.sheet(playerAppearance(other.color), false));
    expect(images[2]?.source).toBe(sprites.sheet(playerAppearance(away.color), true));
    expect(images[0]?.source).not.toBe(images[1]?.source);
  });

  it("AK-1: der Körper liegt dort, wo sonst das Quadrat liegt", () => {
    const withSprites = draw(three, { sprites: spriteSet() });
    const without = draw(three);
    const images = withSprites.images();
    const boxes = squares(without);
    three.forEach((player, i) => {
      const image = images[i];
      const square = boxes[i];
      if (!image || !square) throw new Error("Zeichenoperationen fehlen");
      expect(image.dx + rig.origin.x - PLAYER_SIZE / 2).toBe(square.x);
      expect(image.dy + rig.origin.y - PLAYER_SIZE / 2).toBe(square.y);
      expect(image.dx).toBe(Math.round(player.x - rig.origin.x));
      expect(image.dy).toBe(Math.round(player.y - rig.origin.y));
    });
  });

  it("AK-1: Name an derselben Stelle wie ohne Sprites", () => {
    const withSprites = draw(three, { sprites: spriteSet() }).texts();
    const without = draw(three).texts();
    expect(withSprites).toEqual(without);
    three.forEach((player, i) => {
      expect(withSprites[i]?.text).toBe(player.name);
    });
  });

  it("AK-3, AK-20: ganze Zelle 1:1 an ganzzahligen Koordinaten, ohne Glättung", () => {
    const ctx = draw([own, { ...other, moving: true }, away], { sprites: spriteSet(), now: 1234 });
    expect(ctx.images()).toHaveLength(3);
    for (const image of ctx.images()) {
      for (const value of [image.sx, image.sy, image.dx, image.dy]) expect(Number.isInteger(value)).toBe(true);
      expect(image.sw).toBe(rig.cell.width);
      expect(image.dw).toBe(rig.cell.width);
      expect(image.sh).toBe(rig.cell.height);
      expect(image.dh).toBe(rig.cell.height);
    }
    expect(ctx.imageSmoothingEnabled).toBe(false);
  });

  it.each<[number | undefined, SpriteDirection]>([
    [0, "right"],
    [Math.PI / 2, "down"],
    [Math.PI, "left"],
    [-Math.PI / 2, "up"],
    [undefined, "down"],
  ])("AK-4, AK-6: Blickrichtung %s zeigt die Spalte %s", (facing, direction) => {
    const ctx = draw([{ ...own, facing }], { sprites: spriteSet() });
    expect(ctx.images()[0]?.sx).toBe(column(direction));
  });

  it("AK-7, AK-8: laufend die Zeile des Laufbilds, stehend das Standbild", () => {
    const sprites = spriteSet();
    expect(draw([{ ...own, moving: true }], { sprites, now: 250 }).images()[0]?.sy).toBe((1 + 2) * rig.cell.height);
    for (const now of [0, 250, 999]) {
      expect(draw([{ ...own, moving: false }], { sprites, now }).images()[0]?.sy).toBe(0);
      expect(draw([own], { sprites, now }).images()[0]?.sy).toBe(0);
    }
  });

  it("AK-9: wartende Figur grau, halb durchsichtig, steht still", () => {
    const sprites = spriteSet();
    const ctx = draw([own, { ...away, moving: true }], { sprites, now: 250, facingArrows: true });
    const [ownImage, awayImage] = ctx.images();
    if (!ownImage || !awayImage) throw new Error("Zeichenoperationen fehlen");
    expect(awayImage.source).toBe(sprites.sheet(playerAppearance(away.color), true));
    expect(awayImage.source).not.toBe(sprites.sheet(playerAppearance(away.color), false));
    expect(awayImage.alpha).toBeLessThan(1);
    expect(awayImage.sy).toBe(0);
    expect(ownImage.alpha).toBe(1);
    expect(ctx.paths("fill")[1]?.alpha).toBe(awayImage.alpha);
  });

  it("AK-10: mit facingArrows false kein Pfeil, Figuren trotzdem da", () => {
    const withSprites = draw(three, { sprites: spriteSet(), facingArrows: false });
    expect(withSprites.paths("fill")).toHaveLength(0);
    expect(withSprites.paths("stroke")).toHaveLength(0);
    expect(withSprites.images()).toHaveLength(3);
    const without = draw(three, { facingArrows: false });
    expect(without.paths("fill")).toHaveLength(0);
    expect(without.paths("stroke")).toHaveLength(0);
    expect(squares(without)).toHaveLength(3);
  });

  it("Entscheidung 10: ohne facingArrows und mit facingArrows true Pfeil an jeder Figur mit Blickrichtung", () => {
    expect(draw(three, { sprites: spriteSet() }).paths("fill")).toHaveLength(3);
    expect(draw(three, { sprites: spriteSet(), facingArrows: true }).paths("fill")).toHaveLength(3);
  });

  it("AK-1: Reihenfolge Figur, Pfeil, Name", () => {
    const ctx = draw([own], { sprites: spriteSet(), facingArrows: true });
    const image = ctx.images()[0];
    const stroke = ctx.paths("stroke")[0];
    const fill = ctx.paths("fill")[0];
    const text = ctx.texts()[0];
    if (!image || !stroke || !fill || !text) throw new Error("Zeichenoperationen fehlen");
    expect(ctx.ops.indexOf(image)).toBeLessThan(ctx.ops.indexOf(stroke));
    expect(ctx.ops.indexOf(stroke)).toBeLessThan(ctx.ops.indexOf(fill));
    expect(ctx.ops.indexOf(fill)).toBeLessThan(ctx.ops.indexOf(text));
  });

  it("AK-14: ohne Sprites Quadrate in Spielerfarbe, Wartende grau", () => {
    const ctx = draw([own, away], { sprites: null });
    const boxes = squares(ctx);
    expect(ctx.images()).toHaveLength(0);
    expect(boxes).toHaveLength(2);
    expect(boxes[0]?.style).toBe(toCssColor(own.color));
    expect(boxes[1]?.style).toBe(toCssColor(desaturate(away.color)));
    expect(boxes[1]?.alpha).toBeLessThan(1);
  });

  it("Deckkraft ist nach den Figuren wieder voll", () => {
    const ctx = draw([away], { sprites: spriteSet() });
    expect(ctx.globalAlpha).toBe(1);
  });
});
