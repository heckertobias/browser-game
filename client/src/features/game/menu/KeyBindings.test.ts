import { createElement, createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Action } from "@/generated/browsergame/v1/game_pb";
import { ACTIONS, DEFAULT_BINDINGS, keyLabel, type Bindings } from "@/features/game/input/bindings";
import { t } from "@/features/localization/i18n";
import { KeyBindings, KeyBindingsView, actionName, slotId } from "./KeyBindings";
import { MenuView } from "./Menu";
import { IDLE, fill, type KeyBindingsState } from "./keyBindingsLogic";

const { MOVE_UP: UP, MOVE_DOWN: DOWN } = Action;
const noop = () => {};
const withUp = (...codes: string[]): Bindings => new Map(DEFAULT_BINDINGS).set(UP, codes);
const escaped = (text: string) => renderToStaticMarkup(createElement("span", null, text)).replace(/^<span>|<\/span>$/g, "");

const view = (over: Partial<{ bindings: Bindings | null; state: KeyBindingsState; layout: Map<string, string> }> = {}) =>
  renderToStaticMarkup(
    createElement(KeyBindingsView, { bindings: DEFAULT_BINDINGS, state: IDLE, layout: undefined, onEvent: noop, ...over }),
  );

function openingTag(markup: string, testId: string): string {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${testId}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${testId}" fehlt`);
  return tag;
}

function classes(tag: string): string[] {
  return (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);
}

function inner(markup: string, testId: string): string {
  const start = markup.indexOf(openingTag(markup, testId));
  const from = markup.indexOf(">", start) + 1;
  return markup.slice(from, markup.slice(from).search(/<\/(button|p)>/) + from);
}

const slot = (action: Action, n: number) => `binding-${String(action)}-${String(n)}`;
const clearId = (action: Action, n: number) => `binding-clear-${String(action)}-${String(n)}`;

describe("Tastenbelegung (002 AK-5 bis AK-11)", () => {
  it("zeigt ohne Belegung den Verbindungsstatus statt der Tabelle", () => {
    const markup = view({ bindings: null });
    expect(markup).toContain('data-testid="binding-loading"');
    expect(markup).not.toContain('data-testid="binding-reset"');
    expect(markup).not.toContain("<table");
  });

  it("hat eine Zeile je Aktion in Reihenfolge", () => {
    const markup = view();
    const positions = ACTIONS.map((action) => markup.indexOf(`data-testid="binding-row-${String(action)}"`));
    expect(positions.every((i) => i >= 0)).toBe(true);
    expect([...positions].sort((a, b) => a - b)).toEqual(positions);
    for (const action of ACTIONS) expect(markup).toContain(`>${escaped(actionName(action))}<`);
    expect(actionName(UP)).toBe(t.actionMoveUp);
    expect(actionName(Action.MOVE_RIGHT)).toBe(t.actionMoveRight);
  });

  it("zeigt die Tasten je Platz", () => {
    const markup = view({ bindings: withUp("KeyW", "ArrowUp") });
    expect(inner(markup, slot(UP, 0))).toBe(keyLabel("KeyW"));
    expect(inner(markup, slot(UP, 1))).toBe(keyLabel("ArrowUp"));
    expect(inner(view(), slot(UP, 1))).toBe("—");
  });

  it("nutzt die Layout-Map für die Namen", () => {
    const layout = new Map([["KeyW", "z"]]);
    expect(inner(view({ layout }), slot(UP, 0))).toBe(keyLabel("KeyW", layout));
  });

  it("nutzt echte Bedienelemente mit Touch-Ziel", () => {
    const markup = view();
    for (const action of ACTIONS) {
      for (const n of [0, 1]) {
        for (const id of [slot(action, n), clearId(action, n)]) {
          const tag = openingTag(markup, id);
          expect(tag.startsWith("<button")).toBe(true);
          expect(tag).toContain('type="button"');
          expect(classes(tag)).toContain("min-h-11");
        }
        expect(openingTag(markup, clearId(action, n))).toContain(`aria-label="${t.bindingClear}"`);
        expect(openingTag(markup, slot(action, n))).toContain(`id="${slotId(action, n as 0 | 1)}"`);
      }
    }
  });

  it("sperrt × nur bei leeren Plätzen (AK-11)", () => {
    const single = view();
    expect(openingTag(single, clearId(UP, 0))).not.toContain("disabled");
    expect(openingTag(single, clearId(UP, 1))).toContain("disabled");
    expect(openingTag(view({ bindings: withUp() }), clearId(UP, 0))).toContain("disabled");
    const open = view({ bindings: withUp("KeyW", "ArrowUp") });
    expect(openingTag(open, clearId(UP, 0))).not.toContain("disabled");
    expect(openingTag(open, clearId(UP, 1))).not.toContain("disabled");
  });

  it("markiert den aufnehmenden Platz", () => {
    const markup = view({ state: { mode: "recording", action: UP, slot: 1 } });
    const tag = openingTag(markup, slot(UP, 1));
    expect(inner(markup, slot(UP, 1))).toBe(t.bindingPressKey);
    expect(tag).toContain('aria-pressed="true"');
    expect(classes(tag)).toContain("btn-primary");
    expect(openingTag(markup, slot(UP, 0))).toContain('aria-pressed="false"');
  });

  it("fragt bei einem Konflikt nach (AK-7)", () => {
    const markup = view({ state: { mode: "confirm", action: DOWN, slot: 1, code: "KeyW", other: UP } });
    expect(openingTag(markup, "binding-confirm")).toBeTruthy();
    expect(markup).toContain(escaped(fill(t.bindingConflict, { key: keyLabel("KeyW"), action: actionName(UP) })));
    expect(markup).toContain(`>${t.bindingOverwrite}<`);
    expect(markup).toContain(`>${t.bindingCancel}<`);
    for (const id of ["binding-overwrite", "binding-cancel"]) {
      expect(openingTag(markup, id).startsWith("<button")).toBe(true);
      expect(openingTag(markup, id)).toContain('type="button"');
    }
    expect(markup).not.toContain('data-testid="binding-notice"');
  });

  it("zeigt Hinweise (AK-8)", () => {
    const notice = (n: Extract<KeyBindingsState, { mode: "idle" }>["notice"]) => view({ state: { mode: "idle", notice: n } });
    const chat = notice({ kind: "reserved", reservedFor: "chat" });
    expect(openingTag(chat, "binding-notice")).toContain('role="status"');
    expect(inner(chat, "binding-notice")).toBe(escaped(t.bindingReservedChat));
    expect(inner(notice({ kind: "reserved", reservedFor: "menu" }), "binding-notice")).toBe(escaped(t.bindingReserved));
    expect(view()).not.toContain('data-testid="binding-notice"');
  });

  it("bietet Standard wiederherstellen an (AK-10)", () => {
    const markup = view();
    const tag = openingTag(markup, "binding-reset");
    expect(tag.startsWith("<button")).toBe(true);
    expect(tag).toContain('type="button"');
    expect(inner(markup, "binding-reset")).toBe(t.bindingReset);
  });

  it("startet die Komponente im Leerlauf", () => {
    const markup = renderToStaticMarkup(createElement(KeyBindings, { bindings: DEFAULT_BINDINGS, onSave: noop }));
    expect(markup).toContain('data-testid="binding-reset"');
    expect(markup).not.toContain('data-testid="binding-notice"');
    expect(markup).not.toContain('data-testid="binding-confirm"');
  });

  it("sitzt im Menü zwischen Überschrift und Zurück", () => {
    const menu = (page: "main" | "keyBindings") =>
      renderToStaticMarkup(
        createElement(MenuView, {
          page,
          focusRef: createRef<HTMLButtonElement>(),
          onContinue: noop,
          onShowKeyBindings: noop,
          onBack: noop,
          keyBindings: createElement("span", { "data-testid": "slot-probe" }),
        }),
      );
    const markup = menu("keyBindings");
    const probe = markup.indexOf('data-testid="slot-probe"');
    expect(markup.indexOf("<h2")).toBeLessThan(probe);
    expect(probe).toBeLessThan(markup.indexOf('data-testid="menu-back"'));
    expect(menu("main")).not.toContain("slot-probe");
  });
});
