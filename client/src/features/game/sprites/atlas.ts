import { fail, integer, record, text } from "./parse";

export interface AtlasEntry {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  /** Punkt im Rechteck, der auf der Weltposition liegt; fehlt er in der Datei, ist er 0, 0. */
  readonly origin: { readonly x: number; readonly y: number };
}

export interface Atlas {
  readonly image: string;
  readonly entries: ReadonlyMap<string, AtlasEntry>;
}

function parseEntry(value: unknown, path: string): AtlasEntry {
  const e = record(value, path);
  const x = integer(e["x"], `${path}.x`, 0);
  const y = integer(e["y"], `${path}.y`, 0);
  const width = integer(e["width"], `${path}.width`, 1);
  const height = integer(e["height"], `${path}.height`, 1);
  const originData = e["origin"];
  if (originData === undefined) return { x, y, width, height, origin: { x: 0, y: 0 } };
  const o = record(originData, `${path}.origin`);
  const origin = { x: integer(o["x"], `${path}.origin.x`, 0), y: integer(o["y"], `${path}.origin.y`, 0) };
  if (origin.x >= width) fail(`${path}.origin.x`, "muss kleiner als width sein");
  if (origin.y >= height) fail(`${path}.origin.y`, "muss kleiner als height sein");
  return { x, y, width, height, origin };
}

/** Atlas des Sprite-Formats (ADR 0018, 018): ein Bild und benannte Rechtecke darin. Wirft Error mit Fundstelle. */
export function parseAtlas(data: unknown): Atlas {
  const root = record(data, "atlas");
  const image = text(root["image"], "image");
  const list = Object.entries(record(root["entries"], "entries"));
  if (list.length === 0) fail("entries", "mindestens ein Eintrag erwartet");
  const entries = new Map(list.map(([name, value]) => [name, parseEntry(value, `entries.${name}`)] as const));
  return { image, entries };
}

/** Namen der Form `name.<…>`, nach UTF-16-Codeeinheiten sortiert (in jedem Browser gleich, AK-3). */
export function group(atlas: Atlas, name: string): readonly string[] {
  const prefix = `${name}.`;
  return [...atlas.entries.keys()].filter((key) => key.startsWith(prefix)).sort();
}
