import { describe, expect, it } from "vitest";
import { type RemotePlayer, SnapshotBuffer } from "@/features/game/state/interpolation";

const deg = (d: number) => (d * Math.PI) / 180;
const at = (id: string, x: number, facing: number, spawnSeq = 0): RemotePlayer => ({
  id,
  name: id,
  color: 0,
  x,
  y: 0,
  spawnSeq,
  facing,
});

describe("SnapshotBuffer – Blickrichtung (016 AK-7)", () => {
  it("interpoliert die Blickrichtung zwischen zwei Snapshots", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0)]);
    buffer.push(50, [at("a", 10, Math.PI / 2)]);
    const sampled = buffer.sample(25)[0];
    expect(sampled?.facing).toBeCloseTo(Math.PI / 4);
    expect(sampled?.x).toBe(5);
  });

  it("dreht über ±π den kürzeren Bogen", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, deg(170))]);
    buffer.push(50, [at("a", 0, deg(-170))]);
    expect(buffer.sample(25)[0]?.facing).toBeCloseTo(Math.PI);
    expect(buffer.sample(12.5)[0]?.facing).toBeCloseTo(deg(175));
  });

  it("AK-3: bleibt im Stehen bei der letzten Richtung", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 3, 1)]);
    buffer.push(50, [at("a", 3, 1)]);
    expect(buffer.sample(25)[0]?.facing).toBeCloseTo(1);
  });

  it("springt bei neuem spawnSeq ohne Zwischenwerte (012 AK-8)", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0, 0)]);
    buffer.push(50, [at("a", 100, Math.PI / 2, 1)]);
    expect(buffer.sample(25)).toEqual([at("a", 100, Math.PI / 2, 1)]);
  });

  it("neue Figur zeigt ihre Blickrichtung sofort", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, []);
    buffer.push(50, [at("b", 7, 2)]);
    expect(buffer.sample(25)).toEqual([at("b", 7, 2)]);
  });

  it("nach dem neuesten Snapshot gilt dessen Blickrichtung", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [at("a", 0, 0)]);
    buffer.push(50, [at("a", 10, 1)]);
    expect(buffer.sample(500)[0]?.facing).toBe(1);
  });
});
