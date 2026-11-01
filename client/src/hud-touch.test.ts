import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import indexHtml from "../index.html?raw";
import offlineHtml from "../offline.html?raw";
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

/** Öffnendes Tag des ersten Elements dieses Namens. */
function firstTag(html: string, name: string): string {
  const tag = new RegExp(`<${name}\\b[^>]*>`).exec(html)?.[0];
  if (tag === undefined) throw new Error(`<${name}> fehlt`);
  return tag;
}

function viewportContent(html: string): string[] {
  const content = /<meta\s+name="viewport"\s+content="([^"]*)"/.exec(html)?.[1];
  if (content === undefined) throw new Error("Viewport-Meta fehlt");
  return content.split(",").map((part) => part.trim());
}

function metaContent(html: string, name: string): string {
  const content = new RegExp(`<meta\\s+name="${name}"\\s+content="([^"]*)"`).exec(html)?.[1];
  if (content === undefined) throw new Error(`Meta ${name} fehlt`);
  return content;
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

describe("Statusleiste auf dem iPhone (003 T13b)", () => {
  const markup = renderToStaticMarkup(createElement(App));

  it("legt die Ansicht unter die Statusleiste, auch nach dem Drehen (007 AK-5, AK-10)", () => {
    expect(metaContent(indexHtml, "apple-mobile-web-app-status-bar-style")).toBe("black-translucent");
  });

  it("hält die HUD-Zeile von Statusleiste und Aussparung fern (007 AK-6)", () => {
    const row = classes(/<div[^>]*top-\[max\(0\.5rem,env\(safe-area-inset-top\)\)\][^>]*>/.exec(markup)?.[0] ?? "");
    expect(row).toEqual(
      expect.arrayContaining([
        "top-[max(0.5rem,env(safe-area-inset-top))]",
        "left-[max(0.5rem,env(safe-area-inset-left))]",
        "right-[max(0.5rem,env(safe-area-inset-right))]",
      ]),
    );
  });
});

describe("Spielbild bis zum unteren Rand (003 T13c)", () => {
  const markup = renderToStaticMarkup(createElement(App));

  it("füllt in der Homescreen-App den ganzen Bildschirm, auch nach dem Drehen (007 AK-5)", () => {
    expect(classes(openingTag(markup, "screen"))).toEqual(
      expect.arrayContaining(["fixed", "inset-0", "bg-black", "standalone:bottom-auto", "standalone:h-screen"]),
    );
  });

  it("Spielbild und HUD wachsen mit dem Container (007 AK-5, AK-6)", () => {
    expect(classes(openingTag(markup, "game"))).toEqual(expect.arrayContaining(["h-full", "w-full"]));
    expect(classes(openingTag(markup, "hud"))).toEqual(expect.arrayContaining(["absolute", "inset-0"]));
  });

  it("hält die Versionsanzeige vom unteren Rand fern (007 AK-6)", () => {
    expect(classes(openingTag(markup, "version"))).toEqual(
      expect.arrayContaining([
        "bottom-[max(0.5rem,env(safe-area-inset-bottom))]",
        "right-[max(0.5rem,env(safe-area-inset-right))]",
      ]),
    );
  });

  it("färbt eine Restlücke schwarz statt in Seitenfarbe (007 AK-6)", () => {
    expect(classes(firstTag(indexHtml, "html"))).toContain("bg-black");
    expect(classes(firstTag(offlineHtml, "html"))).toContain("bg-black");
  });

  it("füllt auch die Offline-Seite und hält Abstand zu den Systemleisten (007 AK-6, AK-12)", () => {
    expect(classes(firstTag(offlineHtml, "main"))).toEqual(
      expect.arrayContaining([
        "fixed",
        "inset-0",
        "standalone:bottom-auto",
        "standalone:h-screen",
        "pt-[max(1rem,env(safe-area-inset-top))]",
        "pb-[max(1rem,env(safe-area-inset-bottom))]",
        "pl-[max(1rem,env(safe-area-inset-left))]",
        "pr-[max(1rem,env(safe-area-inset-right))]",
      ]),
    );
  });
});
