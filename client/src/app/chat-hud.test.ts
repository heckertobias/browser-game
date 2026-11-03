import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("Chat im HUD (011 AK-1)", () => {
  const markup = renderToStaticMarkup(createElement(App));
  const tag = /<[^>]*\bdata-testid="chat"[^>]*>/.exec(markup)?.[0] ?? "";
  const classes = (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);

  it("liegt in der HUD-Ebene", () => {
    expect(markup.indexOf('data-testid="hud"')).toBeGreaterThanOrEqual(0);
    expect(markup.indexOf('data-testid="chat"')).toBeGreaterThan(markup.indexOf('data-testid="hud"'));
  });

  it("nimmt Klicks an, sitzt unten links und ist halbtransparent", () => {
    expect(classes).toContain("pointer-events-auto");
    expect(classes.some((c) => c.startsWith("bottom-["))).toBe(true);
    expect(classes.some((c) => c.startsWith("left-["))).toBe(true);
    expect(classes).toContain("bg-black/50");
  });
});
