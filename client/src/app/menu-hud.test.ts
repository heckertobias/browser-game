import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { t } from "@/features/localization/i18n";
import { App } from "./App";

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

describe("Menü-Button im HUD (002 AK-2)", () => {
  const markup = renderToStaticMarkup(createElement(App));

  it("ist ein bedienbarer Button im HUD", () => {
    const tag = openingTag(markup, "menu-button");
    expect(tag.startsWith("<button")).toBe(true);
    expect(tag).toContain('type="button"');
    expect(classes(tag)).toEqual(expect.arrayContaining(["pointer-events-auto", "min-h-11"]));
    expect(markup.indexOf('data-testid="menu-button"')).toBeGreaterThan(markup.indexOf('data-testid="hud"'));
    expect(markup).toContain(`>${t.menu}<`);
  });

  it("ersetzt den Link zum Account", () => {
    expect(markup).not.toContain('href="/Account/Manage"');
  });

  it("startet mit geschlossenem Menü", () => {
    expect(markup).not.toContain('data-testid="menu"');
    expect(openingTag(markup, "hud")).not.toContain("inert");
  });
});
