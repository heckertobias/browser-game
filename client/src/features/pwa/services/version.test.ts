import { describe, expect, it } from "vitest";
import { formatVersion } from "./version";

describe("formatVersion", () => {
  it("zeigt dev ohne Version", () => {
    expect(formatVersion(undefined)).toBe("dev");
    expect(formatVersion("")).toBe("dev");
    expect(formatVersion("  ")).toBe("dev");
  });

  it("gibt Versionen und Commit-Hashes unverändert zurück", () => {
    expect(formatVersion("0.4.0-rc.3")).toBe("0.4.0-rc.3");
    expect(formatVersion("0.4.0")).toBe("0.4.0");
    expect(formatVersion("5539bc0")).toBe("5539bc0");
  });
});
