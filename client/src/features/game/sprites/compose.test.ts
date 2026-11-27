import { describe, expect, it } from "vitest";
import { desaturate } from "@/features/game/render/render";
import { compose, greyscale, tint } from "./compose";
import { blank, paint, pixelAt, type Rgba } from "./testPixels";

function single(rgba: Rgba) {
  const p = blank(1, 1);
  paint(p, 0, 0, rgba);
  return p;
}

describe("tint (017)", () => {
  it("AK-2: Grau 255 ergibt die Farbe selbst, für zwei Farben", () => {
    expect(pixelAt(tint(single([255, 255, 255, 255]), 0x3366cc), 0, 0)).toEqual([0x33, 0x66, 0xcc, 255]);
    expect(pixelAt(tint(single([255, 255, 255, 255]), 0xff8800), 0, 0)).toEqual([255, 136, 0, 255]);
  });

  it("AK-2: Grau wirkt anteilig", () => {
    expect(pixelAt(tint(single([128, 128, 128, 255]), 0x3366cc), 0, 0)).toEqual([26, 51, 102, 255]);
    expect(pixelAt(tint(single([0, 0, 0, 255]), 0x3366cc), 0, 0)).toEqual([0, 0, 0, 255]);
  });

  it("lässt Alpha, Eingabe und Array unverändert", () => {
    const input = single([255, 255, 255, 0]);
    const result = tint(input, 0x3366cc);
    expect(pixelAt(result, 0, 0)[3]).toBe(0);
    expect(pixelAt(input, 0, 0)).toEqual([255, 255, 255, 0]);
    expect(result.data).not.toBe(input.data);
  });
});

describe("compose (017)", () => {
  const A: Rgba = [1, 2, 3, 255];
  const B: Rgba = [4, 5, 6, 255];
  const C: Rgba = [7, 8, 9, 255];

  function row(a: Rgba | undefined, b: Rgba | undefined) {
    const p = blank(2, 1);
    if (a !== undefined) paint(p, 0, 0, a);
    if (b !== undefined) paint(p, 1, 0, b);
    return p;
  }

  it("AK-18: obere Ebene ersetzt nur deckende Pixel", () => {
    const result = compose([row(A, B), row(C, undefined)]);
    expect(pixelAt(result, 0, 0)).toEqual(C);
    expect(pixelAt(result, 1, 0)).toEqual(B);
  });

  it("AK-18: Alpha 0 und Alpha 128 lassen das untere stehen", () => {
    const result = compose([row(A, B), row([9, 9, 9, 0], [9, 9, 9, 128])]);
    expect(pixelAt(result, 0, 0)).toEqual(A);
    expect(pixelAt(result, 1, 0)).toEqual(B);
  });

  it("AK-19: bei drei Ebenen gewinnt die oberste deckende", () => {
    const result = compose([row(A, A), row(B, B), row(C, undefined)]);
    expect(pixelAt(result, 0, 0)).toEqual(C);
    expect(pixelAt(result, 1, 0)).toEqual(B);
  });

  it("startet transparent", () => {
    expect(pixelAt(compose([row([9, 9, 9, 0], undefined)]), 0, 0)).toEqual([0, 0, 0, 0]);
  });

  it("AK-20: Eckpixel bleiben erhalten", () => {
    const corners = blank(4, 4);
    for (const [x, y] of [[0, 0], [3, 0], [0, 3], [3, 3]] as const) paint(corners, x, y, A);
    const middle = blank(4, 4);
    paint(middle, 1, 1, B);
    const result = compose([corners, middle]);
    for (const [x, y] of [[0, 0], [3, 0], [0, 3], [3, 3]] as const) expect(pixelAt(result, x, y)).toEqual(A);
    expect(pixelAt(result, 1, 1)).toEqual(B);
  });

  it("wirft bei leerer Liste und abweichender Größe", () => {
    expect(() => compose([])).toThrow();
    expect(() => compose([blank(2, 1), blank(1, 2)])).toThrow();
  });

  it("lässt Eingaben unverändert und liefert ein neues Array", () => {
    const lower = row(A, B);
    const result = compose([lower]);
    expect(result.data).not.toBe(lower.data);
    expect(pixelAt(lower, 0, 0)).toEqual(A);
  });
});

describe("greyscale (017)", () => {
  it("AK-9: Rec. 601 für die Grundfarben", () => {
    expect(pixelAt(greyscale(single([255, 0, 0, 255])), 0, 0)).toEqual([76, 76, 76, 255]);
    expect(pixelAt(greyscale(single([0, 255, 0, 255])), 0, 0)).toEqual([150, 150, 150, 255]);
    expect(pixelAt(greyscale(single([0, 0, 255, 255])), 0, 0)).toEqual([29, 29, 29, 255]);
  });

  it("AK-9: gleiche Graustufe wie desaturate", () => {
    for (const color of [0x3366cc, 0xff8800, 0x123456]) {
      const grey = desaturate(color);
      const l = grey & 0xff;
      const rgba: Rgba = [(color >> 16) & 0xff, (color >> 8) & 0xff, color & 0xff, 255];
      expect(pixelAt(greyscale(single(rgba)), 0, 0)).toEqual([l, l, l, 255]);
    }
  });

  it("lässt Alpha und Eingabe unverändert", () => {
    const input = single([10, 200, 30, 0]);
    expect(pixelAt(greyscale(input), 0, 0)[3]).toBe(0);
    expect(pixelAt(input, 0, 0)).toEqual([10, 200, 30, 0]);
  });
});
