import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Action } from "@/generated/browsergame/v1/game_pb";
import { DEFAULT_BINDINGS, keyLabel } from "@/features/game/input/bindings";
import { t } from "@/features/localization/i18n";
import { KeyBindingsView, actionName } from "./KeyBindings";
import { IDLE } from "./keyBindingsLogic";

const { ATTACK_MELEE: MELEE, ATTACK_RANGED: RANGED, MOVE_RIGHT: RIGHT } = Action;
const noop = () => {};
const markup = renderToStaticMarkup(
  createElement(KeyBindingsView, { bindings: DEFAULT_BINDINGS, state: IDLE, layout: undefined, onEvent: noop }),
);

function openingTag(testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function inner(testId: string): string {
  const start = markup.indexOf(openingTag(testId));
  const from = markup.indexOf(">", start) + 1;
  return markup.slice(from, markup.slice(from).search(/<\/(button|p)>/) + from);
}

const slot = (action: Action, n: number) => `binding-${String(action)}-${String(n)}`;
const clearId = (action: Action, n: number) => `binding-clear-${String(action)}-${String(n)}`;
const row = (action: Action) => markup.indexOf(`data-testid="binding-row-${String(action)}"`);

describe("Tastenbelegung: Angriffe (013, 002 Menü)", () => {
  it("benennt die Angriffe", () => {
    expect(actionName(MELEE)).toBe(t.actionAttackMelee);
    expect(actionName(RANGED)).toBe(t.actionAttackRanged);
  });

  it("zeigt je Angriff eine Zeile nach den Bewegungen mit Standardtaste", () => {
    expect(row(RIGHT)).toBeGreaterThan(-1);
    expect(row(MELEE)).toBeGreaterThan(row(RIGHT));
    expect(row(RANGED)).toBeGreaterThan(row(MELEE));
    expect(inner(slot(MELEE, 0))).toBe(keyLabel("Digit1"));
    expect(inner(slot(RANGED, 0))).toBe(keyLabel("Digit2"));
    expect(openingTag(clearId(MELEE, 0))).not.toContain("disabled");
    expect(openingTag(clearId(MELEE, 1))).toContain("disabled");
  });
});
