export type SpriteDirection = "down" | "up" | "left" | "right";

/** Die vier Richtungen, die ein Rig kennen darf (ADR 0018). */
export const SPRITE_DIRECTIONS: readonly SpriteDirection[] = ["down", "up", "left", "right"];

export interface RigAnimation {
  readonly firstRow: number;
  readonly frames: number;
  readonly frameMs?: number;
}

export interface RigPart {
  readonly slot: string;
  readonly image: string;
  readonly tint?: boolean;
}

export interface Rig {
  readonly cell: { readonly width: number; readonly height: number };
  readonly origin: { readonly x: number; readonly y: number };
  readonly directions: readonly SpriteDirection[];
  readonly animations: Readonly<Record<string, RigAnimation>>;
  readonly slots: readonly string[];
  readonly parts: Readonly<Record<string, RigPart>>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isSpriteDirection(value: unknown): value is SpriteDirection {
  return SPRITE_DIRECTIONS.some((d) => d === value);
}

function fail(path: string, expected: string): never {
  throw new Error(`${path}: ${expected}`);
}

function record(value: unknown, path: string): Record<string, unknown> {
  return isRecord(value) ? value : fail(path, "Objekt erwartet");
}

function integer(value: unknown, path: string, min: number): number {
  return typeof value === "number" && Number.isInteger(value) && value >= min
    ? value
    : fail(path, `ganze Zahl >= ${String(min)} erwartet`);
}

function text(value: unknown, path: string): string {
  return typeof value === "string" && value !== "" ? value : fail(path, "nicht leerer Text erwartet");
}

function parseAnimation(value: unknown, path: string): RigAnimation {
  const a = record(value, path);
  const firstRow = integer(a["firstRow"], `${path}.firstRow`, 0);
  const frames = integer(a["frames"], `${path}.frames`, 1);
  const frameMs = a["frameMs"];
  if (frameMs === undefined) {
    return frames > 1 ? fail(`${path}.frameMs`, "Zahl > 0 erwartet (Pflicht bei frames > 1)") : { firstRow, frames };
  }
  if (typeof frameMs !== "number" || !Number.isFinite(frameMs) || frameMs <= 0) {
    return fail(`${path}.frameMs`, "Zahl > 0 erwartet");
  }
  return { firstRow, frames, frameMs };
}

function parsePart(value: unknown, path: string, slots: readonly string[]): RigPart {
  const p = record(value, path);
  const slot = p["slot"];
  if (typeof slot !== "string" || !slots.includes(slot)) fail(`${path}.slot`, "unbekannter Slot");
  const image = text(p["image"], `${path}.image`);
  const tint = p["tint"];
  if (tint === undefined) return { slot, image };
  return typeof tint === "boolean" ? { slot, image, tint } : fail(`${path}.tint`, "true oder false erwartet");
}

export function parseRig(data: unknown): Rig {
  const root = record(data, "rig");

  const cellData = record(root["cell"], "cell");
  const cell = {
    width: integer(cellData["width"], "cell.width", 1),
    height: integer(cellData["height"], "cell.height", 1),
  };

  const originData = record(root["origin"], "origin");
  const origin = {
    x: integer(originData["x"], "origin.x", 0),
    y: integer(originData["y"], "origin.y", 0),
  };
  if (origin.x >= cell.width) fail("origin.x", "muss kleiner als cell.width sein");
  if (origin.y >= cell.height) fail("origin.y", "muss kleiner als cell.height sein");

  const directionsData = root["directions"];
  if (!Array.isArray(directionsData) || directionsData.length === 0) fail("directions", "nicht leere Liste erwartet");
  const directions: SpriteDirection[] = [];
  directionsData.forEach((d: unknown, i) => {
    if (!isSpriteDirection(d)) fail(`directions.${String(i)}`, "down, up, left oder right erwartet");
    if (directions.includes(d)) fail(`directions.${String(i)}`, "doppelt");
    directions.push(d);
  });

  const animationsData = record(root["animations"], "animations");
  const animationEntries = Object.entries(animationsData);
  if (animationEntries.length === 0) fail("animations", "mindestens eine Animation erwartet");
  const animations = Object.fromEntries(
    animationEntries.map(([name, value]) => [name, parseAnimation(value, `animations.${name}`)]),
  );

  const slotsData = root["slots"];
  if (!Array.isArray(slotsData)) fail("slots", "Liste erwartet");
  const slots: string[] = [];
  slotsData.forEach((s: unknown, i) => {
    const slot = text(s, `slots.${String(i)}`);
    if (slots.includes(slot)) fail(`slots.${String(i)}`, "doppelt");
    slots.push(slot);
  });

  const parts = Object.fromEntries(
    Object.entries(record(root["parts"], "parts")).map(([name, value]) => [name, parsePart(value, `parts.${name}`, slots)]),
  );

  return { cell, origin, directions, animations, slots, parts };
}

/** Format-Regel 1: Spalten je Richtung, Zeilen bis zur letzten Animation. */
export function sheetSize(rig: Rig): { width: number; height: number } {
  const rows = Math.max(...Object.values(rig.animations).map((a) => a.firstRow + a.frames));
  return { width: rig.directions.length * rig.cell.width, height: rows * rig.cell.height };
}

export function frameRect(rig: Rig, animation: string, direction: SpriteDirection, now: number): { sx: number; sy: number } {
  const column = rig.directions.indexOf(direction);
  if (column === -1) throw new Error(`directions: ${direction} fehlt`);
  const anim = Object.hasOwn(rig.animations, animation) ? rig.animations[animation] : undefined;
  if (anim === undefined) throw new Error(`animations.${animation}: unbekannt`);
  const frame = anim.frames === 1 || anim.frameMs === undefined ? 0 : Math.floor(now / anim.frameMs) % anim.frames;
  return { sx: column * rig.cell.width, sy: (anim.firstRow + frame) * rig.cell.height };
}
