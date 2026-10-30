import { describe, expect, it } from "vitest";
import { SnapshotBuffer, type RemotePlayer } from "./interpolation";

const player = (id: string, x: number, y = 0): RemotePlayer => ({ id, name: id, color: 0, x, y });

describe("SnapshotBuffer (AK-3, AK-4)", () => {
  it("ist ohne Snapshots leer", () => {
    expect(new SnapshotBuffer().sample(0)).toEqual([]);
  });

  it("interpoliert zwischen den einschließenden Snapshots", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [player("a", 0)]);
    buffer.push(50, [player("a", 10)]);

    expect(buffer.sample(25)).toEqual([player("a", 5)]);
  });

  it("zeigt nach dem neuesten Snapshot dessen Positionen", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [player("a", 0)]);
    buffer.push(50, [player("a", 10)]);

    expect(buffer.sample(500)).toEqual([player("a", 10)]);
  });

  it("neue Spieler erscheinen ohne Interpolation", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, []);
    buffer.push(50, [player("b", 7)]);

    expect(buffer.sample(25)).toEqual([player("b", 7)]);
  });

  it("AK-4: wer im späteren Snapshot fehlt, wird nicht mehr gezeigt", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(0, [player("a", 0), player("b", 0)]);
    buffer.push(50, [player("b", 0)]);

    expect(buffer.sample(25).map((p) => p.id)).toEqual(["b"]);
  });

  it("verwirft alte Snapshots", () => {
    const buffer = new SnapshotBuffer();
    for (let t = 0; t <= 5000; t += 50) buffer.push(t, [player("a", t)]);

    expect(buffer.sample(0)).toEqual([player("a", 3950)]); // älter als 1 s ist weg
  });
});
