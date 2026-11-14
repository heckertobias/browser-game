import { Children, createElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AttackRejected_Reason, Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import type { AttackRejectReason } from "@/features/game/network/net";
import { t } from "@/features/localization/i18n";
import { ATTACK_HINT_MS, AttackButtonsView, AttackHint, hintText, type AttackButtonsViewProps } from "./AttackButtons";

const { NOT_ENOUGH_MANA, NOT_FACING } = AttackRejected_Reason;
const noop = () => {};
const escaped = (text: string) => renderToStaticMarkup(createElement("span", null, text)).replace(/^<span>|<\/span>$/g, "");

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

interface Probe { children?: ReactNode; "data-testid"?: string; onClick?: () => void }

function findByTestId(node: ReactNode, id: string): ReactElement<Probe> | undefined {
  for (const child of Children.toArray(node)) {
    if (!isValidElement<Probe>(child)) continue;
    if (child.props["data-testid"] === id) return child;
    const found = findByTestId(child.props.children, id);
    if (found) return found;
  }
  return undefined;
}

const view = (over: Partial<AttackButtonsViewProps> = {}) =>
  renderToStaticMarkup(createElement(AttackButtonsView, { hasTarget: true, hint: null, onAttack: noop, ...over }));

describe("hintText (AK-8, AK-19)", () => {
  it("liefert den Text zum Grund", () => {
    expect(hintText(NOT_ENOUGH_MANA)).toBe(t.notEnoughMana);
    expect(hintText(NOT_FACING)).toBe(t.notFacing);
  });
});

describe("AttackHint (AK-8, AK-19)", () => {
  const onChange = vi.fn<(r: AttackRejectReason | null) => void>();
  let hint: AttackHint;

  beforeEach(() => {
    vi.useFakeTimers();
    onChange.mockClear();
    hint = new AttackHint(onChange);
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

describe("AttackButtonsView (plan.md Entscheidung 9)", () => {
  it("zwei bedienbare Buttons mit Daumengröße", () => {
    const markup = view();
    for (const id of ["attack-melee", "attack-ranged"]) {
      const tag = openingTag(markup, id);
      expect(tag.startsWith("<button")).toBe(true);
      expect(tag).toContain('type="button"');
      expect(classes(tag)).toContain("min-h-14");
    }
    expect(markup).toContain(`>${escaped(t.attackMelee)}<`);
    expect(markup).toContain(`>${escaped(t.attackRanged)}<`);
    expect(markup.indexOf('data-testid="attack-melee"')).toBeLessThan(markup.indexOf('data-testid="attack-ranged"'));
  });

  it("AK-2: ohne Ziel ausgegraut", () => {
    const without = view({ hasTarget: false });
    const withTarget = view({ hasTarget: true });
    for (const id of ["attack-melee", "attack-ranged"]) {
      expect(openingTag(without, id)).toContain("disabled");
      expect(openingTag(withTarget, id)).not.toContain("disabled");
    }
  });

  it("die Leiste fängt den Zeiger", () => {
    const markup = view();
    expect(classes(openingTag(markup, "attack-bar"))).toContain("pointer-events-auto");
    expect(classes(openingTag(markup, "attack-buttons"))).not.toContain("pointer-events-auto");
  });

  it("Klick löst die passende Angriffsart aus", () => {
    const onAttack = vi.fn<(kind: Attack_Kind) => void>();
    const tree = AttackButtonsView({ hasTarget: true, hint: null, onAttack });
    findByTestId(tree, "attack-melee")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.MELEE);
    findByTestId(tree, "attack-ranged")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.RANGED);
    expect(onAttack).toHaveBeenCalledTimes(2);
  });

  it("ohne Hinweis kein Statusfeld", () => {
    expect(view()).not.toContain('data-testid="attack-hint"');
  });

  it("AK-8: Hinweis zu wenig Mana über den Buttons", () => {
    const markup = view({ hint: NOT_ENOUGH_MANA });
    expect(openingTag(markup, "attack-hint")).toContain('role="status"');
    expect(markup).toContain(`>${escaped(t.notEnoughMana)}<`);
    expect(markup.indexOf('data-testid="attack-hint"')).toBeLessThan(markup.indexOf('data-testid="attack-melee"'));
  });

  it("AK-19: Hinweis Ziel nicht vor dir", () => {
    const markup = view({ hint: NOT_FACING });
    expect(markup).toContain(`>${escaped(t.notFacing)}<`);
    expect(markup).not.toContain(escaped(t.notEnoughMana));
  });

  it("Hinweis auch ohne Ziel sichtbar", () => {
    expect(view({ hasTarget: false, hint: NOT_FACING })).toContain('data-testid="attack-hint"');
  });
});
