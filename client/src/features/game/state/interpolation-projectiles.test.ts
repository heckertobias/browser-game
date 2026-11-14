import { describe, expect, it } from "vitest";
import { SnapshotBuffer } from "./interpolation";

interface Projectile {
  id: number;
  x: number;
  y: number;
}

describe("SnapshotBuffer: Projektile (013)", () => {
  it("AK-7: interpoliert numerische IDs zwischen zwei Snapshots, ohne Blickrichtung", () => {
    const buffer = new SnapshotBuffer<Projectile>();
    buffer.push(0, [{ id: 1, x: 0, y: 0 }]);
    buffer.push(50, [{ id: 1, x: 20, y: 10 }]);
    const sampled = buffer.sample(25);
    expect(sampled).toEqual([{ id: 1, x: 10, y: 5 }]);
    expect("facing" in (sampled[0] ?? {})).toBe(false);
  });

  it("ein neues Projektil erscheint ohne Interpolation an seiner Position", () => {
    const buffer = new SnapshotBuffer<Projectile>();
    buffer.push(0, []);
    buffer.push(50, [{ id: 2, x: 30, y: 40 }]);
    expect(buffer.sample(25)).toEqual([{ id: 2, x: 30, y: 40 }]);
  });

  it("nach dem Einschlag ist das Projektil nicht mehr enthalten", () => {
    const buffer = new SnapshotBuffer<Projectile>();
    buffer.push(0, [{ id: 1, x: 0, y: 0 }]);
    buffer.push(50, []);
    expect(buffer.sample(25)).toEqual([]);
  });

  it("mehrere Projektile werden getrennt interpoliert", () => {
    const buffer = new SnapshotBuffer<Projectile>();
    buffer.push(0, [{ id: 1, x: 0, y: 0 }, { id: 2, x: 100, y: 0 }]);
    buffer.push(50, [{ id: 1, x: 10, y: 0 }, { id: 2, x: 80, y: 0 }]);
    expect(buffer.sample(25)).toEqual([{ id: 1, x: 5, y: 0 }, { id: 2, x: 90, y: 0 }]);
  });
});
