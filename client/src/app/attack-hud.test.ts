import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

describe("Angriffs-Buttons im HUD (013 plan.md Entscheidung 9)", () => {
  const markup = renderToStaticMarkup(createElement(App));

  it("liegen im HUD und starten ohne Ziel gesperrt (AK-2)", () => {
    expect(markup.indexOf('data-testid="attack-buttons"')).toBeGreaterThan(markup.indexOf('data-testid="hud"'));
    expect(openingTag(markup, "attack-melee")).toContain("disabled");
    expect(openingTag(markup, "attack-ranged")).toContain("disabled");
  });

  it("ohne Ablehnung kein Hinweis", () => {
    expect(markup).not.toContain('data-testid="attack-hint"');
  });

  it("liegen vor der Versionsanzeige im Markup", () => {
    expect(markup.indexOf('data-testid="attack-buttons"')).toBeLessThan(markup.indexOf('data-testid="version"'));
  });
});

describe("Stapelrang (013, Chat über den Buttons)", () => {
  it("der Chat steht nach den Buttons im Markup und überdeckt sie", () => {
    const markup = renderToStaticMarkup(createElement(App));
    expect(markup.indexOf('data-testid="chat"')).toBeGreaterThan(markup.indexOf('data-testid="attack-buttons"'));
  });
});
