import type { Pixels } from "./compose";

export type Rgba = readonly [number, number, number, number];

export function blank(width: number, height: number): Pixels {
  return { width, height, data: new Uint8ClampedArray(width * height * 4) };
}

export function paint(pixels: Pixels, x: number, y: number, rgba: Rgba): void {
  pixels.data.set(rgba, (y * pixels.width + x) * 4);
}

export function pixelAt(pixels: Pixels, x: number, y: number): Rgba {
  const i = (y * pixels.width + x) * 4;
  return [pixels.data[i] ?? 0, pixels.data[i + 1] ?? 0, pixels.data[i + 2] ?? 0, pixels.data[i + 3] ?? 0];
}
