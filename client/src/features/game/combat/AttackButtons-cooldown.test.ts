import { Children, createElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { AttackButtonsView, type AttackButtonsViewProps } from "./AttackButtons";
import { sweepBackground } from "./cooldownSweep";

const noop = () => {};

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

/** Der Sweep steht zwischen dem öffnenden Tag des Buttons und dem nächsten `</button>`. */
function inside(markup: string, buttonId: string, sweepId: string): boolean {
  const start = markup.indexOf(`data-testid="${buttonId}"`);
  const end = markup.indexOf("</button>", start);
  const sweep = markup.indexOf(`data-testid="${sweepId}"`);
  return start >= 0 && sweep > start && sweep < end;
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

describe("AttackButtonsView: Cooldown-Sweep (013 AK-22)", () => {
  it("ohne Abklingzeit kein Sweep", () => {
    for (const markup of [view(), view({ cooldown: 0 })]) {
      expect(markup).not.toContain('data-testid="attack-melee-cooldown"');
      expect(markup).not.toContain('data-testid="attack-ranged-cooldown"');
    }
  });

  it("Sweep auf beiden Buttons", () => {
    const markup = view({ cooldown: 0.5 });
    expect(inside(markup, "attack-melee", "attack-melee-cooldown")).toBe(true);
    expect(inside(markup, "attack-ranged", "attack-ranged-cooldown")).toBe(true);
    for (const id of ["attack-melee-cooldown", "attack-ranged-cooldown"]) {
      const tag = openingTag(markup, id);
      expect(tag).toContain('aria-hidden="true"');
      expect(classes(tag)).toEqual(expect.arrayContaining(["absolute", "inset-0", "pointer-events-none"]));
      expect(tag).toContain(sweepBackground(0.5));
    }
  });

  it("Buttons sind Bezug für den Sweep", () => {
    const markup = view({ cooldown: 0.5 });
    for (const id of ["attack-melee", "attack-ranged"]) {
      expect(classes(openingTag(markup, id))).toContain("relative");
    }
  });

  it("während der Abklingzeit klickbar", () => {
    const markup = view({ hasTarget: true, cooldown: 0.5 });
    for (const id of ["attack-melee", "attack-ranged"]) {
      expect(openingTag(markup, id)).not.toContain("disabled");
    }
    const onAttack = vi.fn<(kind: Attack_Kind) => void>();
    const tree = AttackButtonsView({ hasTarget: true, onAttack, cooldown: 0.5 });
    findByTestId(tree, "attack-melee")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.MELEE);
    findByTestId(tree, "attack-ranged")?.props.onClick?.();
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.RANGED);
  });

  it("Sweep auch ohne Ziel", () => {
    const markup = view({ hasTarget: false, cooldown: 0.5 });
    expect(markup).toContain('data-testid="attack-melee-cooldown"');
    expect(markup).toContain('data-testid="attack-ranged-cooldown"');
    for (const id of ["attack-melee", "attack-ranged"]) {
      expect(openingTag(markup, id)).toContain("disabled");
    }
  });
});
