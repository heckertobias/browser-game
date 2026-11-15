import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("Cooldown-Sweep im HUD (013 AK-22)", () => {
  it("beim Start kein Sweep", () => {
    const markup = renderToStaticMarkup(createElement(App));
    expect(markup).toContain('data-testid="attack-melee"');
    expect(markup).not.toContain('data-testid="attack-melee-cooldown"');
    expect(markup).not.toContain('data-testid="attack-ranged-cooldown"');
  });
});
