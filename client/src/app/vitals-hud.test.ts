import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("HP und Mana im HUD (012 AK-5)", () => {
  it("zeigt vor dem ersten Snapshot keine Werte", () => {
    expect(renderToStaticMarkup(createElement(App))).not.toContain('data-testid="vitals"');
  });
});
