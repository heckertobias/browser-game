import { describe, expect, it } from "vitest";
import { SnapshotBuffer, type RemotePlayer } from "./interpolation";

const at = (id: string, x: number, y: number, spawnSeq: number, away = false): RemotePlayer => ({
  id,
  name: id,
  color: 0,
  x,
  y,
  away,
  spawnSeq,
});

describe("SnapshotBuffer – Sprünge (012 AK-8)", () => {
  it("springt ohne Zwischenwerte, wenn sich spawnSeq ändert", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, [at("a", 100, 40, 1)]);
    expect(buffer.sample(25)).toEqual([at("a", 100, 40, 1)]);
    expect(buffer.sample(1)).toEqual([at("a", 100, 40, 1)]);
  });

  it("interpoliert bei gleichem spawnSeq ungleich 0 weiter", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 2)]);
    buffer.push(50, [at("a", 10, 0, 2)]);
    expect(buffer.sample(25)).toEqual([at("a", 5, 0, 2)]);
  });

  it("nur die gesprungene Figur springt", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0), at("b", 0, 0, 0)]);
    buffer.push(50, [at("a", 100, 0, 1), at("b", 10, 0, 0)]);
    expect(buffer.sample(25)).toEqual([at("a", 100, 0, 1), at("b", 5, 0, 0)]);
  });

  it("vor dem Sprung wird normal interpoliert", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, [at("a", 10, 0, 0)]);
    buffer.push(100, [at("a", 200, 0, 1)]);
    expect(buffer.sample(25)).toEqual([at("a", 5, 0, 0)]);
    expect(buffer.sample(75)).toEqual([at("a", 200, 0, 1)]);
  });

  it("nach dem Sprung wird wieder interpoliert", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, [at("a", 100, 0, 1)]);
    buffer.push(100, [at("a", 110, 0, 1)]);
    expect(buffer.sample(75)).toEqual([at("a", 105, 0, 1)]);
  });

  it("away-Figur springt ebenso und bleibt away", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0, true)]);
    buffer.push(50, [at("a", 100, 0, 1, true)]);
    expect(buffer.sample(25)).toEqual([at("a", 100, 0, 1, true)]);
  });

  it("neu auftauchende Figur mit spawnSeq > 0 erscheint an ihrer Position", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, []);
    buffer.push(50, [at("a", 7, 3, 3)]);
    expect(buffer.sample(25)).toEqual([at("a", 7, 3, 3)]);
  });

  it("nach Fehlen in einem Snapshot taucht die Figur ohne Gleiten wieder auf", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, []);
    buffer.push(100, [at("a", 300, 0, 0)]);
    expect(buffer.sample(25)).toEqual([]);
    expect(buffer.sample(75)).toEqual([at("a", 300, 0, 0)]);
  });
});
