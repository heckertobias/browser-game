import { readdirSync, readFileSync } from "node:fs";
import { PNG } from "pngjs";
import { describe, expect, it } from "vitest";
import { PLAYER_SIZE } from "@/features/game/core/constants";
import type { Pixels } from "./compose";
import { parseRig, sheetSize, type Rig } from "./rig";
import { HUMANOID_IMAGES, HUMANOID_RIG, loadSpriteSet, playerAppearance } from "./spriteSet";
import { pixelAt } from "./testPixels";

const DIR = new URL("../../../assets/sprites/humanoid/", import.meta.url);

function readPixels(file: string): Pixels {
  const png = PNG.sync.read(readFileSync(new URL(file, DIR)));
  return { width: png.width, height: png.height, data: new Uint8ClampedArray(png.data) };
}

const fileRig: unknown = JSON.parse(readFileSync(new URL("rig.json", DIR), "utf8"));
const rig: Rig = parseRig(fileRig);
const partImages = Object.entries(rig.parts).map(([name, part]) => ({ name, part, pixels: readPixels(part.image) }));
const folderImages = readdirSync(DIR)
  .filter((file) => file.endsWith(".png"))
  .sort();
const partFiles = Object.values(rig.parts)
  .map((part) => part.image)
  .sort();

function opaque(p: Pixels, x: number, y: number): boolean {
  return p.data[(y * p.width + x) * 4 + 3] === 255;
}

function forEachOpaque(p: Pixels, visit: (x: number, y: number) => void): void {
  for (let y = 0; y < p.height; y++) for (let x = 0; x < p.width; x++) if (opaque(p, x, y)) visit(x, y);
}

const toSource = (pixels: Pixels): CanvasImageSource => ({ pixels }) as unknown as CanvasImageSource;
const pixelsOf = (source: CanvasImageSource): Pixels => (source as unknown as { pixels: Pixels }).pixels;

describe("humanoid assets (017)", () => {
  it("rig.json ist gültig und HUMANOID_RIG ist dieselbe Datei", () => {
    expect(() => parseRig(fileRig)).not.toThrow();
    expect(parseRig(HUMANOID_RIG)).toEqual(rig);
  });

  it("hat idle, walk, vier Richtungen und die Teile Körper und Kleidung", () => {
    expect(Object.keys(rig.animations)).toEqual(expect.arrayContaining(["idle", "walk"]));
    expect([...rig.directions].sort()).toEqual(["down", "left", "right", "up"]);
    expect(rig.slots).toEqual(expect.arrayContaining(["body", "clothing"]));
    expect(rig.slots.indexOf("body")).toBeLessThan(rig.slots.indexOf("clothing"));
    expect(rig.parts["body/default"]?.slot).toBe("body");
    expect(rig.parts["body/default"]?.tint).not.toBe(true);
    expect(rig.parts["clothing/ninja"]?.slot).toBe("clothing");
    expect(rig.parts["clothing/ninja"]?.tint).toBe(true);
  });

  it("jedes Teil hat die Blattgröße des Rigs", () => {
    const size = sheetSize(rig);
    for (const { name, pixels } of partImages) {
      expect({ name, width: pixels.width, height: pixels.height }).toEqual({ name, ...size });
    }
  });

  it("Alpha ist nur 0 oder 255", () => {
    for (const { name, pixels } of partImages) {
      for (let y = 0; y < pixels.height; y++) {
        for (let x = 0; x < pixels.width; x++) {
          const alpha = pixelAt(pixels, x, y)[3];
          expect([0, 255], `${name} bei ${String(x)},${String(y)}`).toContain(alpha);
        }
      }
    }
  });

  it("einfärbbare Teile sind Graustufen mit hellstem Wert 255", () => {
    for (const { name, part, pixels } of partImages) {
      if (part.tint !== true) continue;
      let max = 0;
      forEachOpaque(pixels, (x, y) => {
        const [r, g, b] = pixelAt(pixels, x, y);
        expect([g, b], `${name} bei ${String(x)},${String(y)}`).toEqual([r, r]);
        max = Math.max(max, r);
      });
      expect(max, name).toBe(255);
    }
  });

  it("kein Bild im Ordner ohne Eintrag in parts", () => {
    expect(folderImages).toEqual(partFiles);
  });

  it("HUMANOID_IMAGES nennt genau die Bilder des Ordners", () => {
    expect(Object.keys(HUMANOID_IMAGES).sort()).toEqual(folderImages);
    for (const url of Object.values(HUMANOID_IMAGES)) expect(url).not.toBe("");
  });

  it("die Figur liegt in jeder Zelle im mittleren Quadrat der Größe PLAYER_SIZE", () => {
    const half = PLAYER_SIZE / 2;
    for (const { name, pixels } of partImages.filter(({ name }) => name === "body/default" || name === "clothing/ninja")) {
      forEachOpaque(pixels, (x, y) => {
        const cx = x % rig.cell.width;
        const cy = y % rig.cell.height;
        const inside = cx >= rig.origin.x - half && cx < rig.origin.x + half && cy >= rig.origin.y - half && cy < rig.origin.y + half;
        expect(inside, `${name} bei ${String(x)},${String(y)}`).toBe(true);
      });
    }
  });

  it("jede Zelle zeigt eine Figur", () => {
    const rows = sheetSize(rig).height / rig.cell.height;
    for (let column = 0; column < rig.directions.length; column++) {
      for (let row = 0; row < rows; row++) {
        const shown = partImages.some(({ pixels }) => {
          for (let y = 0; y < rig.cell.height; y++) {
            for (let x = 0; x < rig.cell.width; x++) {
              if (opaque(pixels, column * rig.cell.width + x, row * rig.cell.height + y)) return true;
            }
          }
          return false;
        });
        expect(shown, `Spalte ${String(column)}, Zeile ${String(row)}`).toBe(true);
      }
    }
  });

  it("die Zelle lässt rund um den Körper mindestens PLAYER_SIZE Platz", () => {
    const half = PLAYER_SIZE / 2;
    expect(rig.origin.x - half).toBeGreaterThanOrEqual(PLAYER_SIZE);
    expect(rig.cell.width - (rig.origin.x + half)).toBeGreaterThanOrEqual(PLAYER_SIZE);
    expect(rig.origin.y - half).toBeGreaterThanOrEqual(PLAYER_SIZE);
    expect(rig.cell.height - (rig.origin.y + half)).toBeGreaterThanOrEqual(PLAYER_SIZE);
  });

  it("lädt mit den echten Bildern und färbt nur die Kleidung", async () => {
    const decode = (url: string): Promise<Pixels> => {
      const name = Object.keys(HUMANOID_IMAGES).find((key) => HUMANOID_IMAGES[key] === url);
      return name === undefined ? Promise.reject(new Error(url)) : Promise.resolve(readPixels(name));
    };
    const set = await loadSpriteSet(HUMANOID_RIG, HUMANOID_IMAGES, decode, toSource);
    if (set === null) throw new Error("SpriteSet nicht geladen");
    const red = pixelsOf(set.sheet(playerAppearance(0xff0000), false));
    const blue = pixelsOf(set.sheet(playerAppearance(0x0000ff), false));
    const clothing = partImages.find(({ name }) => name === "clothing/ninja")?.pixels;
    if (clothing === undefined) throw new Error("clothing/ninja fehlt");
    for (let y = 0; y < red.height; y++) {
      for (let x = 0; x < red.width; x++) {
        const grey = pixelAt(clothing, x, y);
        const where = `${String(x)},${String(y)}`;
        if (grey[3] === 0) {
          expect(pixelAt(red, x, y), where).toEqual(pixelAt(blue, x, y));
        } else if (grey[0] === 255) {
          expect(pixelAt(red, x, y), where).toEqual([255, 0, 0, 255]);
          expect(pixelAt(blue, x, y), where).toEqual([0, 0, 255, 255]);
        }
      }
    }
  });
});
