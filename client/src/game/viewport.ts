import { MAX_ASPECT_LONG, MAX_ASPECT_SHORT, MAX_SHORT_SIDE, TARGET_SHORT_SIDE } from "./constants";

/** Canvas in Gerätepixeln, ganzzahliger Faktor und sichtbare Welt (ADR 0010). */
export interface Viewport {
  /** Gerätepixel je Weltpixel, ganzzahlig ≥ 1. */
  scale: number;
  /** Gerätepixel = ganzes Fenster. */
  canvasWidth: number;
  canvasHeight: number;
  /** Sichtbare Welt in Weltpixeln (darf gebrochen sein). */
  viewWidth: number;
  viewHeight: number;
  /** Gerätepixel bis zum Spielbild; > 0 nur über 21:9 (AK-4). */
  offsetX: number;
  offsetY: number;
}

/** Faktor für die kurze Seite: relativ am nächsten an 360 (Grenze: geometrisches Mittel), nie über 400. */
export function chooseScale(shortSide: number): number {
  const ratio = shortSide / TARGET_SHORT_SIDE;
  let scale = Math.max(1, Math.floor(ratio));
  if (ratio > Math.sqrt(scale * (scale + 1))) scale++;
  if (shortSide / scale > MAX_SHORT_SIDE) scale++;
  return scale;
}

export function fitViewport(deviceWidth: number, deviceHeight: number): Viewport {
  const width = Math.max(1, Math.round(deviceWidth));
  const height = Math.max(1, Math.round(deviceHeight));
  const short = Math.min(width, height);
  const scale = chooseScale(short);
  const maxLong = Math.floor((short * MAX_ASPECT_LONG) / MAX_ASPECT_SHORT);
  const drawWidth = width >= height ? Math.min(width, maxLong) : width;
  const drawHeight = width >= height ? height : Math.min(height, maxLong);
  return {
    scale,
    canvasWidth: width,
    canvasHeight: height,
    viewWidth: drawWidth / scale,
    viewHeight: drawHeight / scale,
    offsetX: Math.floor((width - drawWidth) / 2),
    offsetY: Math.floor((height - drawHeight) / 2),
  };
}

/** Bequemlichkeit für Tests und den Fallback ohne device-pixel-content-box. */
export function computeViewport(cssWidth: number, cssHeight: number, devicePixelRatio: number): Viewport {
  return fitViewport(cssWidth * devicePixelRatio, cssHeight * devicePixelRatio);
}
