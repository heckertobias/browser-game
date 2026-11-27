export interface Pixels {
  readonly width: number;
  readonly height: number;
  readonly data: Uint8ClampedArray;
}

/** Format-Regel 6: Grauwert (Rotkanal) mal Farbe, je Kanal. Alpha bleibt. */
export function tint(pixels: Pixels, color: number): Pixels {
  const channels = [(color >> 16) & 0xff, (color >> 8) & 0xff, color & 0xff];
  const data = pixels.data.slice();
  for (let i = 0; i < data.length; i += 4) {
    const grey = data[i] ?? 0;
    for (let c = 0; c < 3; c++) data[i + c] = Math.round((grey * (channels[c] ?? 0)) / 255);
  }
  return { width: pixels.width, height: pixels.height, data };
}

/** Format-Regel 7: Ebenen von unten nach oben, nur Pixel mit Alpha 255 ersetzen. */
export function compose(layers: readonly Pixels[]): Pixels {
  const first = layers[0];
  if (first === undefined) throw new Error("compose: mindestens eine Ebene erwartet");
  const { width, height } = first;
  const data = new Uint8ClampedArray(width * height * 4);
  layers.forEach((layer, index) => {
    if (layer.width !== width || layer.height !== height) {
      throw new Error(`compose.${String(index)}: Größe ${String(width)}×${String(height)} erwartet`);
    }
    for (let i = 0; i < data.length; i += 4) {
      if (layer.data[i + 3] !== 255) continue;
      data.set(layer.data.subarray(i, i + 4), i);
    }
  });
  return { width, height, data };
}

/** 017 Entscheidung 6: Rec. 601 wie `desaturate` in render.ts. Alpha bleibt. */
export function greyscale(pixels: Pixels): Pixels {
  const data = pixels.data.slice();
  for (let i = 0; i < data.length; i += 4) {
    const l = Math.round(0.299 * (data[i] ?? 0) + 0.587 * (data[i + 1] ?? 0) + 0.114 * (data[i + 2] ?? 0));
    data[i] = l;
    data[i + 1] = l;
    data[i + 2] = l;
  }
  return { width: pixels.width, height: pixels.height, data };
}
