import { Children, createElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { t } from "@/features/localization/i18n";
import { AttackButtonsView, type AttackButtonsViewProps } from "./AttackButtons";

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
  renderToStaticMarkup(createElement(AttackButtonsView, { hasTarget: true, onAttack: noop, ...over }));

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
    const tree = AttackButtonsView({ hasTarget: true, onAttack });
    findByTestId(tree, "attack-melee")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.MELEE);
    findByTestId(tree, "attack-ranged")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.RANGED);
    expect(onAttack).toHaveBeenCalledTimes(2);
  });
});
