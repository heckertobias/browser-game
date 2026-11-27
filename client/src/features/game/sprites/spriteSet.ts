import { compose, greyscale, tint, type Pixels } from "./compose";
import { parseRig, sheetSize, type Rig } from "./rig";

export type Appearance = Readonly<Record<string, { readonly part: string; readonly tint?: number }>>;
export type Decode = (url: string) => Promise<Pixels>;
export type ToSource = (pixels: Pixels) => CanvasImageSource;

const playerAppearances = new Map<number, Appearance>();

/**
 * Einzige Stelle, die Teile beim Namen nennt. Die Namen MÜSSEN zu den Schlüsseln in rig.json passen.
 * Dieselbe Farbe liefert dasselbe Objekt.
 */
export function playerAppearance(color: number): Appearance {
  let appearance = playerAppearances.get(color);
  if (appearance === undefined) {
    appearance = { body: { part: "body/default" }, clothing: { part: "clothing/ninja", tint: color } };
    playerAppearances.set(color, appearance);
  }
  return appearance;
}

/** Eindeutiger, dateinamentauglicher Schlüssel; nur Slots aus `slots` fließen ein. */
export function appearanceKey(appearance: Appearance, slots: readonly string[]): string {
  return slots
    .map((slot) => {
      const entry = Object.hasOwn(appearance, slot) ? appearance[slot] : undefined;
      if (entry === undefined) return "";
      const part = encodeURIComponent(entry.part);
      return entry.tint === undefined ? part : `${part},${entry.tint.toString(16).padStart(6, "0")}`;
    })
    .join("+");
}

export class SpriteSet {
  private readonly cache = new Map<string, CanvasImageSource>();

  constructor(
    readonly rig: Rig,
    private readonly parts: ReadonlyMap<string, Pixels>,
    private readonly toSource: ToSource,
  ) {}

  sheet(appearance: Appearance, away: boolean): CanvasImageSource {
    const key = `${away ? "away" : "live"}:${appearanceKey(appearance, this.rig.slots)}`;
    const cached = this.cache.get(key);
    if (cached !== undefined) return cached;

    const layers: Pixels[] = [];
    for (const slot of this.rig.slots) {
      const entry = Object.hasOwn(appearance, slot) ? appearance[slot] : undefined;
      if (entry === undefined) continue;
      const rigPart = Object.hasOwn(this.rig.parts, entry.part) ? this.rig.parts[entry.part] : undefined;
      if (rigPart?.slot !== slot) continue;
      const pixels = this.parts.get(entry.part);
      if (pixels === undefined) continue;
      layers.push(rigPart.tint === true && entry.tint !== undefined ? tint(pixels, entry.tint) : pixels);
    }

    const { width, height } = sheetSize(this.rig);
    const composed = layers.length === 0 ? { width, height, data: new Uint8ClampedArray(width * height * 4) } : compose(layers);
    const source = this.toSource(away ? greyscale(composed) : composed);
    this.cache.set(key, source);
    return source;
  }
}

/** `null` bei jedem Fehler; kein Fehlerhinweis (017 Entscheidung 11). */
export async function loadSpriteSet(
  rigData: unknown,
  imageUrls: Readonly<Record<string, string>>,
  decode: Decode,
  toSource: ToSource,
): Promise<SpriteSet | null> {
  try {
    const rig = parseRig(rigData);
    const size = sheetSize(rig);
    const entries = await Promise.all(
      Object.entries(rig.parts).map(async ([name, part]) => {
        const url = Object.hasOwn(imageUrls, part.image) ? imageUrls[part.image] : undefined;
        if (url === undefined) throw new Error(`parts.${name}.image: ${part.image} fehlt`);
        const pixels = await decode(url);
        if (pixels.width !== size.width || pixels.height !== size.height) {
          throw new Error(`parts.${name}: Blattgröße ${String(size.width)}×${String(size.height)} erwartet`);
        }
        return [name, pixels] as const;
      }),
    );
    return new SpriteSet(rig, new Map(entries), toSource);
  } catch {
    return null;
  }
}
