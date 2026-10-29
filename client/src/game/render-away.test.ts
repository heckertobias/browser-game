import { describe, expect, it } from "vitest";
import { desaturate } from "./render";

describe("desaturate (AK-20)", () => {
  it.each([
    [0xff0000, 0x4c4c4c],
    [0x00ff00, 0x969696],
    [0x0000ff, 0x1d1d1d],
    [0xffffff, 0xffffff],
    [0x000000, 0x000000],
  ])("%i wird zu %i", (input, expected) => {
    expect(desaturate(input)).toBe(expected);
  });

  it("liefert immer Grau", () => {
    const result = desaturate(0x3a7bd5);
    expect((result >> 16) & 0xff).toBe((result >> 8) & 0xff);
    expect((result >> 8) & 0xff).toBe(result & 0xff);
  });
});
