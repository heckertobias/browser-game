import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import indexHtml from "../../index.html?raw";
import { App } from "./App";

/** Öffnendes Tag des Elements mit diesem data-testid. */
function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

function viewportContent(html: string): string[] {
  const content = /<meta\s+name="viewport"\s+content="([^"]*)"/.exec(html)?.[1];
  if (content === undefined) throw new Error("Viewport-Meta fehlt");
  return content.split(",").map((part) => part.trim());
}

describe("HUD auf dem Handy (003 T13a)", () => {
  const markup = renderToStaticMarkup(createElement(App));

  it("lässt den Link „Mein Account“ nicht ziehen (007 AK-8)", () => {
    const link = openingTag(markup, "account");
    expect(link).toContain('href="/Account/Manage"');
    expect(link).toContain('draggable="false"');
  });

  it("sperrt Gesten auf der HUD-Ebene wie auf dem Spielbild (007 AK-8)", () => {
    const hud = classes(openingTag(markup, "hud"));
    expect(hud).toContain("touch-none");
    expect(hud).toContain("pointer-events-none");
  });

  it("verhindert automatisches Zoomen beim Drehen (007 AK-5, AK-6)", () => {
    expect(viewportContent(indexHtml)).toEqual(
      expect.arrayContaining(["width=device-width", "initial-scale=1", "maximum-scale=1", "viewport-fit=cover", "user-scalable=no"]),
    );
  });
});
