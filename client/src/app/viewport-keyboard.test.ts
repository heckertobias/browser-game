import { describe, expect, it } from "vitest";
import indexHtml from "../../index.html?raw";

describe("Viewport bei offener Tastatur (011 AK-7, 007 AK-1)", () => {
  it("Layout bleibt: resizes-visual, nicht resizes-content", () => {
    const content = /<meta\s+name="viewport"\s+content="([^"]*)"/.exec(indexHtml)?.[1];
    if (content === undefined) throw new Error("Viewport-Meta fehlt");
    const entries = content.split(",").map((entry) => entry.trim());
    expect(entries).toContain("interactive-widget=resizes-visual");
    expect(entries).not.toContain("interactive-widget=resizes-content");
  });
});
