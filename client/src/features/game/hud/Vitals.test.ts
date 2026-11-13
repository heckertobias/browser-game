import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { t } from "@/features/localization/i18n";
import { Vitals, fillPercent, vitalsText } from "./Vitals";

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

const view = (vitals: { hp: number; maxHp: number; mana: number; maxMana: number }) =>
  renderToStaticMarkup(createElement(Vitals, { vitals }));

describe("vitalsText (012 AK-5)", () => {
  it("zeigt Wert und Maximum", () => {
    expect(vitalsText(73, 100)).toBe("73/100");
  });

  it("rundet ab", () => {
    expect(vitalsText(40.75, 100)).toBe("40/100");
    expect(vitalsText(99.99, 100)).toBe("99/100");
  });

  it("zeigt nie negative Werte und kein -0", () => {
    expect(vitalsText(-5, 100)).toBe("0/100");
    expect(vitalsText(-0, 100)).toBe("0/100");
  });
});

describe("fillPercent (012 AK-5)", () => {
  it("liefert den Anteil in Prozent", () => {
    expect(fillPercent(50, 100)).toBe(50);
    expect(fillPercent(0, 100)).toBe(0);
    expect(fillPercent(100, 100)).toBe(100);
    expect(fillPercent(1, 3)).toBeCloseTo(33.333, 2);
  });

  it("begrenzt auf 0 bis 100", () => {
    expect(fillPercent(150, 100)).toBe(100);
    expect(fillPercent(-5, 100)).toBe(0);
  });

  it("liefert ohne gültiges Maximum 0", () => {
    expect(fillPercent(10, 0)).toBe(0);
    expect(fillPercent(10, -1)).toBe(0);
  });
});

describe("Vitals im HUD (012 AK-5, AK-6)", () => {
  const v = { hp: 73, maxHp: 100, mana: 40, maxMana: 100 };
  const markup = view(v);

  it("zeigt beide Werte mit Beschriftung", () => {
    expect(markup).toContain(`>${t.hp} ${vitalsText(73, 100)}<`);
    expect(markup).toContain(`>${t.mana} ${vitalsText(40, 100)}<`);
  });

  it("rendert Balken als progress mit Wert, Maximum und Beschriftung", () => {
    const hp = openingTag(markup, "vitals-hp");
    expect(hp.startsWith("<progress")).toBe(true);
    expect(hp).toContain('value="73"');
    expect(hp).toContain('max="100"');
    expect(hp).toContain(`aria-label="${t.hp}"`);
    expect(hp).toContain(`aria-valuetext="${vitalsText(73, 100)}"`);
    const mana = openingTag(markup, "vitals-mana");
    expect(mana.startsWith("<progress")).toBe(true);
    expect(mana).toContain('value="40"');
    expect(mana).toContain('max="100"');
    expect(mana).toContain(`aria-label="${t.mana}"`);
    expect(mana).toContain(`aria-valuetext="${vitalsText(40, 100)}"`);
  });

  it("färbt HP rot und Mana blau", () => {
    expect(classes(openingTag(markup, "vitals-hp"))).toEqual(expect.arrayContaining(["progress", "progress-error"]));
    expect(classes(openingTag(markup, "vitals-mana"))).toEqual(expect.arrayContaining(["progress", "progress-info"]));
  });

  it("zeigt HP vor Mana", () => {
    expect(markup.indexOf('data-testid="vitals-hp"')).toBeLessThan(markup.indexOf('data-testid="vitals-mana"'));
  });

  it("liegt in einer eigenen Zeile und ist nicht bedienbar", () => {
    expect(classes(openingTag(markup, "vitals"))).toContain("basis-full");
    expect(markup).not.toContain("pointer-events-auto");
    expect(markup).not.toContain("<button");
  });

  it("zeigt einen neuen Wert", () => {
    const next = view({ ...v, mana: 41 });
    expect(next).toContain(vitalsText(41, 100));
    expect(openingTag(next, "vitals-mana")).toContain('value="41"');
  });

  it("füllt relativ zum Maximum", () => {
    const half = view({ hp: 25, maxHp: 50, mana: 0, maxMana: 100 });
    expect(openingTag(half, "vitals-hp")).toContain('value="50"');
    expect(openingTag(half, "vitals-hp")).toContain('max="100"');
    expect(openingTag(half, "vitals-mana")).toContain('value="0"');
  });
});
