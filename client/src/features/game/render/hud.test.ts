import { describe, expect, it } from "vitest";
import { formatCoordinates } from "./hud";

describe("formatCoordinates (AK-19b)", () => {
  it("rundet auf ganze Zahlen", () => {
    expect(formatCoordinates(12.4, -3.6)).toBe("X 12 · Y -4");
  });

  it("zeigt kein -0", () => {
    expect(formatCoordinates(-0.4, 0.4)).toBe("X 0 · Y 0");
  });

  it("lässt ganze Zahlen unverändert", () => {
    expect(formatCoordinates(1000, -250)).toBe("X 1000 · Y -250");
  });
});
