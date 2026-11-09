import { createElement, createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { t } from "@/features/localization/i18n";
import { Menu, MenuView } from "./Menu";
import type { MenuPage } from "./menuLogic";

const noop = () => {};
const view = (page: MenuPage) =>
  renderToStaticMarkup(createElement(MenuView, { page, focusRef: createRef<HTMLButtonElement>(), onContinue: noop, onShowKeyBindings: noop, onBack: noop }));

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

describe("Spielmenü (002 AK-3, AK-4b)", () => {
  const main = view("main");

  it("enthält die Einträge in Reihenfolge", () => {
    const ids = ["menu-continue", "menu-key-bindings", "menu-account", "menu-sign-out"].map((id) => main.indexOf(`data-testid="${id}"`));
    expect(ids.every((i) => i >= 0)).toBe(true);
    expect([...ids].sort((a, b) => a - b)).toEqual(ids);
  });

  it("nutzt echte Bedienelemente", () => {
    for (const id of ["menu-continue", "menu-key-bindings"]) {
      const tag = openingTag(main, id);
      expect(tag.startsWith("<button")).toBe(true);
      expect(tag).toContain('type="button"');
    }
    for (const id of ["menu-account", "menu-sign-out"]) expect(openingTag(main, id).startsWith("<a")).toBe(true);
  });

  it("verlinkt Konto und Abmelden", () => {
    expect(openingTag(main, "menu-account")).toContain('href="/Account/Manage"');
    expect(openingTag(main, "menu-sign-out")).toContain('href="/Account/Logout"');
  });

  it("lässt die Links nicht ziehen (007 AK-8)", () => {
    for (const id of ["menu-account", "menu-sign-out"]) expect(openingTag(main, id)).toContain('draggable="false"');
  });

  it("beschriftet aus i18n", () => {
    for (const text of [t.menuContinue, t.menuKeyBindings, t.myAccount, t.menuSignOut]) expect(main).toContain(`>${text}<`);
  });

  it("hat Touch-Ziele", () => {
    for (const id of ["menu-continue", "menu-key-bindings", "menu-account", "menu-sign-out"]) {
      expect(classes(openingTag(main, id))).toContain("min-h-11");
    }
  });

  it("ist ein halbtransparentes Overlay mit Safe Areas", () => {
    const tag = openingTag(main, "menu");
    expect(tag).toContain('role="dialog"');
    expect(tag).toContain('aria-modal="true"');
    const cls = classes(tag);
    for (const c of ["absolute", "inset-0", "bg-black/60", "overflow-y-auto"]) expect(cls).toContain(c);
    for (const prefix of ["pt-[max(", "pb-[max(", "pl-[max(", "pr-[max("]) {
      expect(cls.some((c) => c.startsWith(prefix) && c.includes("env(safe-area-inset-"))).toBe(true);
    }
  });

  it("zeigt die Unteransicht Tastenbelegung", () => {
    const sub = view("keyBindings");
    expect(sub).toContain("menu-key-bindings-page");
    const back = openingTag(sub, "menu-back");
    expect(back.startsWith("<button")).toBe(true);
    expect(back).toContain('type="button"');
    expect(sub).toContain(`>${t.menuKeyBindings}<`);
    expect(sub).not.toContain("menu-continue");
  });

  it("startet auf der Hauptseite", () => {
    const markup = renderToStaticMarkup(createElement(Menu, { onClose: noop }));
    expect(markup).toContain('data-testid="menu-main"');
    expect(markup).not.toContain("menu-key-bindings-page");
  });
});
