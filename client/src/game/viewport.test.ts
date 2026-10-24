import { describe, expect, it } from "vitest";
import { computeViewport } from "./viewport";

// AK-6: gleicher Weltausschnitt für alle, nur schärfer skaliert; Rest bleibt Rand.
describe("computeViewport", () => {
  it.each([
    [1280, 720, 1, 2],
    [1920, 1080, 1, 3],
    [2560, 1440, 1, 4],
    [3840, 2160, 1, 6],
    [1920, 1080, 2, 6], // Retina/HiDPI zählt in Gerätepixeln
  ])("%ix%i @%ix → Faktor %i", (width, height, dpr, scale) => {
    expect(computeViewport(width, height, dpr)).toEqual({ scale, width: 640 * scale, height: 360 * scale });
  });

  it("nutzt nur ganzzahlige Faktoren und lässt Rand bei anderem Seitenverhältnis", () => {
    const viewport = computeViewport(1000, 1000, 1);
    expect(viewport).toEqual({ scale: 1, width: 640, height: 360 });
  });

  it("verkleinert fraktional, wenn das Fenster kleiner als 640×360 ist", () => {
    const viewport = computeViewport(320, 568, 1);
    expect(viewport.scale).toBeCloseTo(0.5);
    expect(viewport.width).toBe(320);
    expect(viewport.height).toBe(180);
  });

  it("hält das Seitenverhältnis immer bei 16:9", () => {
    for (const [w, h] of [[800, 600], [2560, 1080], [390, 844]] as const) {
      const viewport = computeViewport(w, h, 3);
      expect(viewport.width / viewport.height).toBeCloseTo(16 / 9, 1);
    }
  });
});
