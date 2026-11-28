import { describe, expect, it } from "vitest";
import { SnapshotBuffer, type RemotePlayer } from "@/features/game/state/interpolation";

const at = (id: string, x: number, y = 0, spawnSeq = 0, facing?: number): RemotePlayer => ({
  id,
  name: id,
  color: 0,
  x,
  y,
  spawnSeq,
  ...(facing === undefined ? {} : { facing }),
});

describe("SnapshotBuffer.moving (017 AK-7, AK-8)", () => {
  it("ist ohne Snapshots leer", () => {
    expect([...new SnapshotBuffer().moving(0)]).toEqual([]);
  });

  it("AK-7: enthält eine Figur, deren x sich zwischen den Snapshots ändert", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0)]);
    buffer.push(50, [at("a", 10)]);
    expect([...buffer.moving(25)].sort()).toEqual(["a"]);
  });

  it("AK-7: auch eine reine y-Bewegung zählt", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0)]);
    buffer.push(50, [at("a", 0, 10)]);
    expect([...buffer.moving(25)].sort()).toEqual(["a"]);
  });

  it("AK-8: ohne Positionsänderung leer", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 5)]);
    buffer.push(50, [at("a", 5)]);
    expect([...buffer.moving(25)].sort()).toEqual([]);
  });

  it("AK-8: Drehen ohne Laufen zählt nicht", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 5, 0, 0, 0)]);
    buffer.push(50, [at("a", 5, 0, 0, Math.PI / 2)]);
    expect([...buffer.moving(25)].sort()).toEqual([]);
  });

  it("nur die bewegte Figur", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0), at("b", 0)]);
    buffer.push(50, [at("a", 10), at("b", 0)]);
    expect([...buffer.moving(25)].sort()).toEqual(["a"]);
  });

  it("ein Sprung (anderer spawnSeq) ist kein Laufen", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, [at("a", 100, 0, 1)]);
    expect([...buffer.moving(25)].sort()).toEqual([]);
  });

  it("neu auftauchende oder verschwundene Figuren laufen nicht", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0)]);
    buffer.push(50, [at("b", 10)]);
    expect([...buffer.moving(25)].sort()).toEqual([]);
  });

  it("nimmt dieselben Snapshots wie sample", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0)]);
    buffer.push(50, [at("a", 10)]);
    buffer.push(100, [at("a", 10)]);
    expect([...buffer.moving(25)].sort()).toEqual(["a"]);
    expect([...buffer.moving(75)].sort()).toEqual([]);
    expect(buffer.sample(25)[0]?.x).toBe(5);
    expect(buffer.sample(75)[0]?.x).toBe(10);
  });

  it("vor dem ältesten und nach dem neuesten Snapshot leer", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0)]);
    buffer.push(50, [at("a", 10)]);
    expect([...buffer.moving(-10)].sort()).toEqual([]);
    expect([...buffer.moving(500)].sort()).toEqual([]);
  });
});
