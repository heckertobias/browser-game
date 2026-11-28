import { describe, expect, it, vi } from "vitest";
import { greyscale, type Pixels } from "./compose";
import { parseRig } from "./rig";
import { appearanceKey, loadSpriteSet, playerAppearance, SpriteSet, type Appearance, type Decode } from "./spriteSet";
import { blank, paint, pixelAt, type Rgba } from "./testPixels";

function rigData(): Record<string, unknown> {
  return {
    cell: { width: 4, height: 4 },
    origin: { x: 2, y: 2 },
    directions: ["down", "right"],
    animations: { idle: { firstRow: 0, frames: 1 }, walk: { firstRow: 0, frames: 2, frameMs: 100 } },
    slots: ["body", "clothing", "hat"],
    parts: {
      "body/plain": { slot: "body", image: "body.png" },
      "clothing/robe": { slot: "clothing", image: "robe.png", tint: true },
      "clothing/vest": { slot: "clothing", image: "vest.png", tint: true },
      "hat/corners": { slot: "hat", image: "hat.png" },
    },
  };
}

const BODY: Rgba = [200, 150, 100, 255];
const HAT: Rgba = [10, 20, 30, 255];
const RED: Rgba = [255, 0, 0, 255];
const CLEAR: Rgba = [0, 0, 0, 0];

function sheetWith(paintCell: (p: Pixels, ox: number, oy: number) => void): Pixels {
  const p = blank(8, 8);
  for (const cy of [0, 1]) for (const cx of [0, 1]) paintCell(p, cx * 4, cy * 4);
  return p;
}

function images(): Record<string, Pixels> {
  return {
    "body.png": sheetWith((p, ox, oy) => {
      for (const [x, y] of [[1, 1], [2, 1], [1, 2], [2, 2]] as const) paint(p, ox + x, oy + y, BODY);
    }),
    "robe.png": sheetWith((p, ox, oy) => {
      paint(p, ox + 1, oy + 2, [255, 255, 255, 255]);
      paint(p, ox + 2, oy + 2, [128, 128, 128, 255]);
    }),
    "vest.png": sheetWith((p, ox, oy) => {
      paint(p, ox + 1, oy + 1, [255, 255, 255, 255]);
    }),
    "hat.png": sheetWith((p, ox, oy) => {
      for (const [x, y] of [[0, 0], [3, 0], [0, 3], [3, 3], [2, 2]] as const) paint(p, ox + x, oy + y, HAT);
    }),
  };
}

function build(withoutParts: readonly string[] = []) {
  const rig = parseRig(rigData());
  const pixelsByImage = images();
  const parts = new Map<string, Pixels>();
  for (const [name, part] of Object.entries(rig.parts)) {
    const pixels = pixelsByImage[part.image];
    if (pixels !== undefined && !withoutParts.includes(name)) parts.set(name, pixels);
  }
  const toSource = vi.fn((pixels: Pixels) => ({ pixels }) as unknown as CanvasImageSource);
  return { rig, set: new SpriteSet(rig, parts, toSource), toSource };
}

function pixelsOf(source: CanvasImageSource): Pixels {
  return (source as unknown as { pixels: Pixels }).pixels;
}

const full = (color: number): Appearance => ({
  hat: { part: "hat/corners" },
  clothing: { part: "clothing/robe", tint: color },
  body: { part: "body/plain" },
});

describe("playerAppearance (017)", () => {
  it("AK-18: Körper und getönte Kleidung", () => {
    expect(playerAppearance(0x3366cc)).toEqual({
      body: { part: "body/default" },
      clothing: { part: "clothing/ninja", tint: 0x3366cc },
    });
  });

  it("AK-15: dieselbe Farbe, dasselbe Objekt", () => {
    expect(playerAppearance(0x112233)).toBe(playerAppearance(0x112233));
    expect(playerAppearance(0x112233)).not.toBe(playerAppearance(0x112234));
  });
});

describe("appearanceKey (017)", () => {
  const slots = ["a", "b"];

  it("ist unabhängig von der Schlüsselreihenfolge", () => {
    expect(appearanceKey({ a: { part: "x" }, b: { part: "y" } }, slots)).toBe(
      appearanceKey({ b: { part: "y" }, a: { part: "x" } }, slots),
    );
  });

  it("unterscheidet Farbe, Teil und fehlenden Slot", () => {
    const base = appearanceKey({ a: { part: "x", tint: 1 } }, slots);
    expect(appearanceKey({ a: { part: "x", tint: 2 } }, slots)).not.toBe(base);
    expect(appearanceKey({ a: { part: "z", tint: 1 } }, slots)).not.toBe(base);
    expect(appearanceKey({ a: { part: "x", tint: 1 }, b: { part: "y" } }, slots)).not.toBe(base);
  });

  it("ist eindeutig", () => {
    expect(appearanceKey({ a: { part: "x,1" } }, slots)).not.toBe(appearanceKey({ a: { part: "x", tint: 1 } }, slots));
  });

  it("ignoriert Slots außerhalb von slots", () => {
    expect(appearanceKey({ a: { part: "x" }, other: { part: "y" } }, slots)).toBe(appearanceKey({ a: { part: "x" } }, slots));
  });

  it("hat das dokumentierte Format ohne Schrägstrich", () => {
    const key = appearanceKey(playerAppearance(0x3366cc), ["body", "clothing"]);
    expect(key).toBe("body%2Fdefault+clothing%2Fninja,3366cc");
    expect(key).not.toContain("/");
  });
});

describe("SpriteSet.sheet (017)", () => {
  it("AK-19: Slot-Reihenfolge kommt aus dem Rig", () => {
    const { set } = build();
    const out = pixelsOf(set.sheet(full(0xff0000), false));
    expect(pixelAt(out, 2, 2)).toEqual(HAT);
    expect(pixelAt(out, 1, 2)).toEqual(RED);
    expect(pixelAt(out, 2, 1)).toEqual(BODY);
    expect(pixelAt(out, 0, 1)).toEqual(CLEAR);
  });

  it("AK-19: zweites Kleidungsteil nur über das Aussehen", () => {
    const { set } = build();
    const vest = pixelsOf(set.sheet({ body: { part: "body/plain" }, clothing: { part: "clothing/vest", tint: 0xff0000 } }, false));
    expect(pixelAt(vest, 1, 1)).toEqual(RED);
    expect(pixelAt(vest, 1, 2)).toEqual(BODY);
    const robe = pixelsOf(set.sheet({ body: { part: "body/plain" }, clothing: { part: "clothing/robe", tint: 0xff0000 } }, false));
    expect(pixelAt(robe, 1, 1)).toEqual(BODY);
    expect(pixelAt(robe, 1, 2)).toEqual(RED);
  });

  it("AK-20: Eckpixel jeder Zelle bleiben erhalten", () => {
    const { set } = build();
    const out = pixelsOf(set.sheet(full(0xff0000), false));
    for (const ox of [0, 4]) {
      for (const oy of [0, 4]) {
        for (const [x, y] of [[0, 0], [3, 0], [0, 3], [3, 3]] as const) expect(pixelAt(out, ox + x, oy + y)).toEqual(HAT);
      }
    }
  });

  it("AK-2/AK-18: andere Farbe ändert nur die Kleidungspixel", () => {
    const { set } = build();
    const dressed = (color: number): Appearance => ({
      body: { part: "body/plain" },
      clothing: { part: "clothing/robe", tint: color },
    });
    const a = pixelsOf(set.sheet(dressed(0xff0000), false));
    const b = pixelsOf(set.sheet(dressed(0x0000ff), false));
    const changed: string[] = [];
    for (let y = 0; y < 8; y++) {
      for (let x = 0; x < 8; x++) {
        if (pixelAt(a, x, y).join() !== pixelAt(b, x, y).join()) changed.push(`${String(x % 4)},${String(y % 4)}`);
      }
    }
    expect([...new Set(changed)].sort()).toEqual(["1,2", "2,2"].sort());
    expect(changed).toHaveLength(8);
    expect(pixelAt(a, 2, 1)).toEqual(pixelAt(b, 2, 1));
  });

  it("AK-18: tint an Teil ohne tint: true wirkt nicht", () => {
    const { set } = build();
    const out = pixelsOf(set.sheet({ body: { part: "body/plain", tint: 0xff0000 } }, false));
    expect(pixelAt(out, 1, 1)).toEqual(BODY);
  });

  it("Teil mit tint: true ohne Farbe bleibt grau", () => {
    const { set } = build();
    const out = pixelsOf(set.sheet({ clothing: { part: "clothing/robe" } }, false));
    expect(pixelAt(out, 1, 2)).toEqual([255, 255, 255, 255]);
  });

  it("unbekanntes Teil, falscher Slot und fehlende Pixel lassen den Slot leer", () => {
    const { set } = build();
    const unknown = pixelsOf(set.sheet({ body: { part: "body/plain" }, clothing: { part: "clothing/nope" } }, false));
    expect(pixelAt(unknown, 2, 1)).toEqual(BODY);
    expect(pixelAt(unknown, 1, 2)).toEqual(BODY);
    const wrongSlot = pixelsOf(set.sheet({ body: { part: "hat/corners" } }, false));
    expect(pixelAt(wrongSlot, 0, 0)).toEqual(CLEAR);
    const missing = build(["hat/corners"]).set;
    expect(pixelAt(pixelsOf(missing.sheet({ hat: { part: "hat/corners" } }, false)), 0, 0)).toEqual(CLEAR);
  });

  it("leeres Aussehen ergibt ein transparentes Blatt", () => {
    const { set } = build();
    const out = pixelsOf(set.sheet({}, false));
    expect([out.width, out.height]).toEqual([8, 8]);
    expect(out.data.every((byte) => byte === 0)).toBe(true);
  });

  it("AK-9: away ist die Graustufe des lebenden Blatts", () => {
    const { set } = build();
    const live = pixelsOf(set.sheet(full(0xff8800), false));
    const away = pixelsOf(set.sheet(full(0xff8800), true));
    expect(away).toEqual(greyscale(live));
    for (let i = 0; i < away.data.length; i += 4) {
      expect(away.data[i + 3]).toBe(live.data[i + 3]);
      if (away.data[i + 3] === 255) {
        expect(away.data[i]).toBe(away.data[i + 1]);
        expect(away.data[i + 1]).toBe(away.data[i + 2]);
      }
    }
  });

  it("AK-15: Cache je Aussehen und away", () => {
    const { set, toSource } = build();
    const first = set.sheet(full(0xff0000), false);
    set.sheet(full(0xff0000), false);
    expect(set.sheet(full(0xff0000), false)).toBe(first);
    expect(toSource).toHaveBeenCalledTimes(1);
    set.sheet(full(0xff0000), true);
    expect(toSource).toHaveBeenCalledTimes(2);
    set.sheet(full(0x0000ff), false);
    expect(toSource).toHaveBeenCalledTimes(3);
    set.sheet(full(0xff0000), false);
    set.sheet(full(0xff0000), true);
    expect(toSource).toHaveBeenCalledTimes(3);
  });

  it("hält das übergebene Rig", () => {
    const { set, rig } = build();
    expect(set.rig).toBe(rig);
  });
});

describe("loadSpriteSet (017)", () => {
  const urls = { "body.png": "/u/body.png", "robe.png": "/u/robe.png", "vest.png": "/u/vest.png", "hat.png": "/u/hat.png" };
  const toSource = (pixels: Pixels) => ({ pixels }) as unknown as CanvasImageSource;

  function decoder(overrides: Record<string, Pixels> = {}) {
    const byUrl = new Map<string, Pixels>();
    for (const [image, pixels] of Object.entries(images())) byUrl.set(`/u/${image}`, pixels);
    for (const [url, pixels] of Object.entries(overrides)) byUrl.set(url, pixels);
    return vi.fn<Decode>((url) => {
      const pixels = byUrl.get(url);
      return pixels === undefined ? Promise.reject(new Error("404")) : Promise.resolve(pixels);
    });
  }

  it("liefert ein SpriteSet und decodiert jedes Teil einmal", async () => {
    const decode = decoder();
    const set = await loadSpriteSet(rigData(), urls, decode, toSource);
    expect(set).toBeInstanceOf(SpriteSet);
    expect(decode).toHaveBeenCalledTimes(4);
    for (const url of Object.values(urls)) expect(decode).toHaveBeenCalledWith(url);
    expect(pixelsOf(set?.sheet(full(0xff0000), false) as CanvasImageSource)).toEqual(
      pixelsOf(build().set.sheet(full(0xff0000), false)),
    );
  });

  it("AK-14: ungültige Beschreibung ergibt null ohne decode", async () => {
    const decode = decoder();
    await expect(loadSpriteSet({}, urls, decode, toSource)).resolves.toBeNull();
    const bad = rigData();
    bad["cell"] = { width: 0, height: 4 };
    await expect(loadSpriteSet(bad, urls, decode, toSource)).resolves.toBeNull();
    expect(decode).not.toHaveBeenCalled();
  });

  it("AK-14: fehlende URL ergibt null", async () => {
    const { "hat.png": _omitted, ...rest } = urls;
    await expect(loadSpriteSet(rigData(), rest, decoder(), toSource)).resolves.toBeNull();
  });

  it("AK-14: abgelehntes oder synchron werfendes decode ergibt null", async () => {
    const rejecting = vi.fn<Decode>(() => Promise.reject(new Error("kaputt")));
    await expect(loadSpriteSet(rigData(), urls, rejecting, toSource)).resolves.toBeNull();
    const throwing = vi.fn<Decode>(() => {
      throw new Error("kaputt");
    });
    await expect(loadSpriteSet(rigData(), urls, throwing, toSource)).resolves.toBeNull();
  });

  it("AK-14: falsche Blattgröße ergibt null", async () => {
    await expect(loadSpriteSet(rigData(), urls, decoder({ "/u/hat.png": blank(8, 4) }), toSource)).resolves.toBeNull();
    await expect(loadSpriteSet(rigData(), urls, decoder({ "/u/hat.png": blank(4, 8) }), toSource)).resolves.toBeNull();
  });
});
