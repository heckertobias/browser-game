import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("Hinweis im HUD (013 AK-20)", () => {
  const markup = renderToStaticMarkup(createElement(App));
  const at = (testId: string) => markup.indexOf(`data-testid="${testId}"`);

  it("liegt im HUD", () => {
    expect(at("attack-hint-area")).toBeGreaterThan(at("hud"));
    expect(at("attack-hint-area")).toBeLessThan(at("version"));
  });

  it("beim Start kein Hinweis", () => {
    expect(markup).not.toContain('data-testid="attack-hint"');
  });

  it("liegt nicht mehr bei den Buttons", () => {
    expect(at("attack-hint-area")).toBeGreaterThan(at("chat"));
    expect(at("attack-hint-area")).toBeGreaterThan(at("attack-buttons"));
  });
});
