import { describe, expect, it } from "vitest";
import { MAX_ASPECT_LONG, MAX_ASPECT_SHORT, MAX_SHORT_SIDE } from "../engine/constants";
import { chooseScale, computeViewport, fitViewport, type Viewport } from "./viewport";

function vp(
  scale: number,
  canvasWidth: number,
  canvasHeight: number,
  viewWidth: number,
  viewHeight: number,
  offsetX = 0,
  offsetY = 0,
): Viewport {
  return { scale, canvasWidth, canvasHeight, viewWidth, viewHeight, offsetX, offsetY };
}

interface Row {
  name: string;
  width: number;
  height: number;
  dpr: number;
  expected: Viewport;
}

// ADR 0010: Tabelle der Beispielgeräte.
const rows: readonly Row[] = [
  { name: "1280×720", width: 1280, height: 720, dpr: 1, expected: vp(2, 1280, 720, 640, 360) },
  { name: "1920×1080", width: 1920, height: 1080, dpr: 1, expected: vp(3, 1920, 1080, 640, 360) },
  { name: "2560×1440", width: 2560, height: 1440, dpr: 1, expected: vp(4, 2560, 1440, 640, 360) },
  { name: "4K", width: 3840, height: 2160, dpr: 1, expected: vp(6, 3840, 2160, 640, 360) },
  { name: "HiDPI", width: 1920, height: 1080, dpr: 2, expected: vp(6, 3840, 2160, 640, 360) },
  { name: "1080p im Browserfenster", width: 1920, height: 960, dpr: 1, expected: vp(3, 1920, 960, 640, 320) },
  { name: "Windows 125 %", width: 1536, height: 730, dpr: 1.25, expected: vp(3, 1920, 913, 640, 913 / 3) },
  { name: "1920×1200", width: 1920, height: 1200, dpr: 1, expected: vp(3, 1920, 1200, 640, 400) },
  { name: "MacBook", width: 1512, height: 982, dpr: 2, expected: vp(5, 3024, 1964, 3024 / 5, 1964 / 5) },
  { name: "MacBook Safari-Fenster", width: 1512, height: 860, dpr: 2, expected: vp(5, 3024, 1720, 3024 / 5, 344) },
  { name: "iPhone hochkant", width: 390, height: 844, dpr: 3, expected: vp(3, 1170, 2532, 390, 844) },
  { name: "iPhone quer", width: 844, height: 340, dpr: 3, expected: vp(3, 2532, 1020, 2380 / 3, 340, 76, 0) },
  { name: "iPhone Pro Max hochkant", width: 430, height: 932, dpr: 3, expected: vp(4, 1290, 2796, 322.5, 699) },
  { name: "Android gerundet", width: 412, height: 915, dpr: 2.625, expected: vp(3, 1082, 2402, 1082 / 3, 2402 / 3) },
  { name: "Ultrawide", width: 3440, height: 1440, dpr: 1, expected: vp(4, 3440, 1440, 840, 360, 40, 0) },
  { name: "32:9 5120", width: 5120, height: 1440, dpr: 1, expected: vp(4, 5120, 1440, 840, 360, 880, 0) },
  { name: "32:9 3840", width: 3840, height: 1080, dpr: 1, expected: vp(3, 3840, 1080, 840, 360, 660, 0) },
  { name: "kleines Fenster", width: 600, height: 450, dpr: 1, expected: vp(2, 600, 450, 300, 225) },
  { name: "winziges Fenster", width: 480, height: 270, dpr: 1, expected: vp(1, 480, 270, 480, 270) },
];

describe("computeViewport", () => {
  it.each(rows)("$name", ({ width, height, dpr, expected }) => {
    expect(computeViewport(width, height, dpr)).toEqual(expected);
  });
});

describe("fitViewport", () => {
  it("füllt hochkant bis 21:9 ohne Versatz", () => {
    expect(fitViewport(1080, 2400)).toEqual(vp(3, 1080, 2400, 360, 800));
  });

  it("zentriert hochkant jenseits von 21:9 (AK-4)", () => {
    expect(fitViewport(1080, 3000)).toEqual(vp(3, 1080, 3000, 360, 840, 0, 240));
  });
});

describe("chooseScale", () => {
  // Bis Faktor 4 entscheidet die Obergrenze 400, danach das geometrische Mittel (360·√30 ≈ 1971,8; 360·√42 ≈ 2333,1).
  it.each([
    [1, 1],
    [400, 1],
    [401, 2],
    [800, 2],
    [801, 3],
    [1200, 3],
    [1201, 4],
    [1600, 4],
    [1601, 5],
    [1971, 5],
    [1972, 6],
    [2333, 6],
    [2334, 7],
  ])("kurze Seite %i → Faktor %i", (shortSide, scale) => {
    expect(chooseScale(shortSide)).toBe(scale);
  });
});

describe("Invarianten über viele Fenstergrößen", () => {
  const DPRS = [1, 1.25, 1.5, 2, 2.625, 3] as const;

  it("hält alle Sichtfeld-Regeln ein", () => {
    const violations: string[] = [];
    for (const dpr of DPRS) {
      for (let w = 200; w <= 4000; w += 37) {
        for (let h = 200; h <= 4000; h += 37) {
          const v = computeViewport(w, h, dpr);
          const deviceW = Math.round(w * dpr);
          const deviceH = Math.round(h * dpr);
          const landscape = deviceW >= deviceH;
          const shortDevice = Math.min(deviceW, deviceH);
          const longDevice = Math.max(deviceW, deviceH);
          const shortView = landscape ? v.viewHeight : v.viewWidth;
          const longView = landscape ? v.viewWidth : v.viewHeight;
          const shortOffset = landscape ? v.offsetY : v.offsetX;
          const beyond = longDevice * MAX_ASPECT_SHORT > shortDevice * MAX_ASPECT_LONG;
          const id = `${String(w)}×${String(h)}@${String(dpr)}`;
          const check = (ok: boolean, rule: string): void => {
            if (!ok) violations.push(`${id}: ${rule}`);
          };

          check(Number.isInteger(v.scale) && v.scale >= 1, "Faktor ganzzahlig ≥ 1");
          check(v.canvasWidth === deviceW && v.canvasHeight === deviceH, "Canvas = Gerätegröße");
          check(Math.abs(v.viewWidth * v.scale + 2 * v.offsetX - v.canvasWidth) <= 1, "x-Aufteilung");
          check(Math.abs(v.viewHeight * v.scale + 2 * v.offsetY - v.canvasHeight) <= 1, "y-Aufteilung");
          check(shortView <= MAX_SHORT_SIDE, "kurze Seite ≤ 400");
          check(longView * v.scale <= (shortView * v.scale * MAX_ASPECT_LONG) / MAX_ASPECT_SHORT + 1, "höchstens 21:9");
          // −11 % gegenüber 360 (ADR 0010).
          check(shortDevice < 1601 || shortView >= 320, "kurze Seite ≥ 320 ab 1601 Gerätepixeln");
          check(beyond || (v.offsetX === 0 && v.offsetY === 0), "Versatz nur jenseits von 21:9");
          check(shortOffset === 0, "kein Versatz auf der kurzen Achse");
          check(Math.abs(shortView * v.scale - shortDevice) <= 1e-6, "kurze Seite füllt das Fenster");
        }
      }
    }
    expect(violations).toEqual([]);
  });
});
