import { describe, expect, it } from "vitest";
import { CULL_MARGIN } from "../engine/constants";
import { cameraOrigin, isVisible, screenToWorld } from "./render";
import type { Viewport } from "./viewport";

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

describe("cameraOrigin", () => {
  it("zentriert die Kamera bei ganzzahligem Ergebnis", () => {
    expect(cameraOrigin({ x: 100, y: 50 }, vp(2, 1280, 720, 640, 360))).toEqual({ x: -220, y: -130 });
  });

  it("rundet auf Gerätepixel", () => {
    const origin = cameraOrigin({ x: 10.2, y: 7.7 }, vp(3, 1920, 1080, 640, 360));
    expect(origin.x).toBeCloseTo(-929 / 3);
    expect(origin.y).toBeCloseTo(-517 / 3);
  });

  it("rundet .5 nach oben", () => {
    expect(cameraOrigin({ x: 0, y: 0 }, vp(3, 1920, 913, 640, 913 / 3))).toEqual({ x: -320, y: -152 });
  });

  it("funktioniert im Hochformat", () => {
    const origin = cameraOrigin({ x: 0, y: 0 }, vp(3, 1082, 2402, 1082 / 3, 2402 / 3));
    expect(origin.x).toBeCloseTo(-541 / 3);
    expect(origin.y).toBeCloseTo(-1201 / 3);
  });

  it.each([
    [vp(2, 1280, 720, 640, 360), { x: 13.37, y: -4.21 }],
    [vp(3, 1920, 913, 640, 913 / 3), { x: 99.9, y: 0.4 }],
    [vp(3, 1082, 2402, 1082 / 3, 2402 / 3), { x: -7.77, y: 123.45 }],
  ])("liegt auf Gerätepixeln und hält die Kamera mittig", (viewport, camera) => {
    const origin = cameraOrigin(camera, viewport);
    expect(origin.x * viewport.scale).toBeCloseTo(Math.round(origin.x * viewport.scale));
    expect(origin.y * viewport.scale).toBeCloseTo(Math.round(origin.y * viewport.scale));
    expect(Math.abs(origin.x + viewport.viewWidth / 2 - camera.x)).toBeLessThanOrEqual(0.5 / viewport.scale + 1e-9);
    expect(Math.abs(origin.y + viewport.viewHeight / 2 - camera.y)).toBeLessThanOrEqual(0.5 / viewport.scale + 1e-9);
  });
});

describe("screenToWorld", () => {
  const origin = (viewport: Viewport) => cameraOrigin({ x: 0, y: 0 }, viewport);

  it("bildet die Mitte auf die Kamera ab", () => {
    const viewport = vp(2, 1280, 720, 640, 360);
    const camera = { x: 100, y: 50 };
    expect(screenToWorld(640, 360, 1280, 720, viewport, cameraOrigin(camera, viewport))).toEqual({ x: 100, y: 50 });
  });

  it("bildet die Ecke auf den Ursprung ab", () => {
    const viewport = vp(2, 1280, 720, 640, 360);
    const result = screenToWorld(0, 0, 1280, 720, viewport, cameraOrigin({ x: 100, y: 50 }, viewport));
    expect(result).toEqual({ x: -220, y: -130 });
  });

  it("rechnet CSS-Pixel bei DPR 2 in Gerätepixel um", () => {
    const viewport = vp(6, 3840, 2160, 640, 360);
    const result = screenToWorld(960, 540, 1920, 1080, viewport, cameraOrigin({ x: 100, y: 50 }, viewport));
    expect(result).toEqual({ x: 100, y: 50 });
  });

  it("berücksichtigt den Balken links", () => {
    const viewport = vp(4, 3440, 1440, 840, 360, 40, 0);
    expect(screenToWorld(1720, 720, 3440, 1440, viewport, origin(viewport))).toEqual({ x: 0, y: 0 });
    expect(screenToWorld(40, 0, 3440, 1440, viewport, origin(viewport))).toEqual({ x: -420, y: -180 });
    expect(screenToWorld(0, 0, 3440, 1440, viewport, origin(viewport)).x).toBe(-430);
  });

  it("berücksichtigt breite Balken", () => {
    const viewport = vp(4, 5120, 1440, 840, 360, 880, 0);
    expect(screenToWorld(2560, 720, 5120, 1440, viewport, origin(viewport))).toEqual({ x: 0, y: 0 });
  });

  it("funktioniert bei nicht ganzzahligem Faktor des Fensters", () => {
    const viewport = vp(3, 2532, 1020, 2380 / 3, 340, 76, 0);
    const result = screenToWorld(422, 170, 844, 340, viewport, origin(viewport));
    expect(result.x).toBeCloseTo(0);
    expect(result.y).toBeCloseTo(0);
  });
});

describe("isVisible", () => {
  const viewport = vp(2, 1280, 720, 640, 360);
  const zero = { x: 0, y: 0 };
  const visible = (x: number, y: number, v = viewport) => isVisible({ x, y }, zero, v, CULL_MARGIN);

  it.each([
    [320, 180, true],
    [-64, 180, true],
    [-65, 180, false],
    [704, 180, true],
    [705, 180, false],
    [320, -65, false],
    [320, 424, true],
    [320, 425, false],
    [320, 375, true],
    [-40, 180, true],
  ])("(%d, %d) → %s", (x, y, expected) => {
    expect(visible(x, y)).toBe(expected);
  });

  it("gilt auch für breite Sichtfelder", () => {
    const wide = vp(3, 2532, 1020, 2380 / 3, 340, 76, 0);
    expect(visible(2380 / 3 + 64, 0, wide)).toBe(true);
    expect(visible(2380 / 3 + 64.5, 0, wide)).toBe(false);
  });
});
