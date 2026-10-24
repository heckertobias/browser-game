import { LOGICAL_HEIGHT, LOGICAL_WIDTH } from "./constants";

/** Größe des Canvas in Gerätepixeln und Faktor logisch → Gerät. */
export interface Viewport {
  scale: number;
  width: number;
  height: number;
}

/**
 * Größter ganzzahliger Faktor, mit dem 640×360 ins Fenster passt (scharfe Pixel);
 * nur bei sehr kleinen Fenstern wird fraktional verkleinert. Der Rest bleibt als Rand frei.
 */
export function computeViewport(cssWidth: number, cssHeight: number, devicePixelRatio: number): Viewport {
  const fit = Math.min((cssWidth * devicePixelRatio) / LOGICAL_WIDTH, (cssHeight * devicePixelRatio) / LOGICAL_HEIGHT);
  const scale = fit >= 1 ? Math.floor(fit) : fit;
  return {
    scale,
    width: Math.round(LOGICAL_WIDTH * scale),
    height: Math.round(LOGICAL_HEIGHT * scale),
  };
}
