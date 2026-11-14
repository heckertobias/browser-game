import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AttackRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { t } from "@/features/localization/i18n";
import {
  ATTACK_HINT_MS,
  AttackHintView,
  HintTimer,
  NO_TARGET,
  hintText,
  type HintReason,
} from "./AttackHint";

const { NOT_ENOUGH_MANA, NOT_FACING, OUT_OF_RANGE } = AttackRejected_Reason;
const REASONS: HintReason[] = [NO_TARGET, OUT_OF_RANGE, NOT_FACING, NOT_ENOUGH_MANA];
const escaped = (text: string) => renderToStaticMarkup(createElement("span", null, text)).replace(/^<span>|<\/span>$/g, "");

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

const view = (reason: HintReason | null) => renderToStaticMarkup(createElement(AttackHintView, { reason }));

describe("hintText (AK-8, AK-19)", () => {
  it("liefert den Text zum Grund", () => {
    expect(hintText(NOT_ENOUGH_MANA)).toBe(t.notEnoughMana);
    expect(hintText(NOT_FACING)).toBe(t.notFacing);
  });
});

describe("HintTimer (AK-8, AK-19)", () => {
  const onChange = vi.fn<(r: HintReason | null) => void>();
  let hint: HintTimer;

  beforeEach(() => {
    vi.useFakeTimers();
    onChange.mockClear();
    hint = new HintTimer(onChange);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const calls = () => onChange.mock.calls.map(([r]) => r);

  it("zeigt den Grund sofort und blendet ihn nach ATTACK_HINT_MS aus", () => {
    hint.show(NOT_ENOUGH_MANA);
    expect(calls()).toEqual([NOT_ENOUGH_MANA]);
    vi.advanceTimersByTime(ATTACK_HINT_MS - 1);
    expect(calls()).toEqual([NOT_ENOUGH_MANA]);
    vi.advanceTimersByTime(1);
    expect(calls()).toEqual([NOT_ENOUGH_MANA, null]);
  });

  it("ein neuer Hinweis ersetzt den alten und startet die Zeit neu", () => {
    hint.show(NOT_ENOUGH_MANA);
    vi.advanceTimersByTime(ATTACK_HINT_MS - 500);
    hint.show(NOT_FACING);
    expect(calls()).toEqual([NOT_ENOUGH_MANA, NOT_FACING]);
    vi.advanceTimersByTime(ATTACK_HINT_MS - 1);
    expect(calls()).toEqual([NOT_ENOUGH_MANA, NOT_FACING]);
    vi.advanceTimersByTime(1);
    expect(calls()).toEqual([NOT_ENOUGH_MANA, NOT_FACING, null]);
  });

  it("clear verhindert das Ausblenden", () => {
    hint.show(NOT_FACING);
    hint.clear();
    vi.advanceTimersByTime(5 * ATTACK_HINT_MS);
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("Standarddauer ist 2 s", () => {
    expect(ATTACK_HINT_MS).toBe(2000);
  });
});

describe("hintText (AK-20)", () => {
  it("liefert den Text zu jedem Grund", () => {
    expect(hintText(NO_TARGET)).toBe(t.noTarget);
    expect(hintText(OUT_OF_RANGE)).toBe(t.outOfRange);
    expect(hintText(NOT_FACING)).toBe(t.notFacing);
    expect(hintText(NOT_ENOUGH_MANA)).toBe(t.notEnoughMana);
  });

  it("jeder Grund hat einen eigenen Text", () => {
    expect(new Set(REASONS.map(hintText)).size).toBe(4);
  });
});

describe("HintTimer (AK-20)", () => {
  const onChange = vi.fn<(r: HintReason | null) => void>();

  beforeEach(() => {
    vi.useFakeTimers();
    onChange.mockClear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("ein neuer Hinweis ersetzt den alten, auch kein Ziel und Reichweite", () => {
    const timer = new HintTimer(onChange);
    timer.show(NO_TARGET);
    vi.advanceTimersByTime(500);
    timer.show(OUT_OF_RANGE);
    expect(onChange.mock.calls.map(([r]) => r)).toEqual([NO_TARGET, OUT_OF_RANGE]);
    vi.advanceTimersByTime(ATTACK_HINT_MS - 1);
    expect(onChange.mock.calls.map(([r]) => r)).toEqual([NO_TARGET, OUT_OF_RANGE]);
    vi.advanceTimersByTime(1);
    expect(onChange.mock.calls.map(([r]) => r)).toEqual([NO_TARGET, OUT_OF_RANGE, null]);
  });
});

describe("AttackHintView (AK-20)", () => {
  it("ohne Grund kein Hinweis, die Fläche bleibt", () => {
    const markup = view(null);
    expect(markup).toContain('data-testid="attack-hint-area"');
    expect(markup).not.toContain('data-testid="attack-hint"');
  });

  it.each(REASONS)("zeigt den Text zu %s als Statusmeldung", (reason) => {
    const markup = view(reason);
    expect(openingTag(markup, "attack-hint")).toContain('role="status"');
    expect(markup).toContain(`>${escaped(hintText(reason))}<`);
  });

  it("liegt mittig im oberen Drittel", () => {
    const area = classes(openingTag(view(null), "attack-hint-area"));
    for (const c of ["absolute", "inset-x-0", "top-1/6", "flex", "justify-center"]) expect(area).toContain(c);
  });

  it("fängt keine Eingaben ab", () => {
    const markup = view(NO_TARGET);
    const area = classes(openingTag(markup, "attack-hint-area"));
    expect(area).toContain("pointer-events-none");
    expect(area).not.toContain("pointer-events-auto");
    expect(classes(openingTag(markup, "attack-hint"))).not.toContain("pointer-events-auto");
  });

  it("groß und kontrastreich", () => {
    const box = classes(openingTag(view(NO_TARGET), "attack-hint"));
    for (const c of ["text-2xl", "bg-black/80", "text-warning", "border-2"]) expect(box).toContain(c);
  });

  it("Standarddauer ist 2 s", () => {
    expect(ATTACK_HINT_MS).toBe(2000);
  });
});
