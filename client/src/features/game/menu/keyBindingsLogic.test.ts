import { describe, expect, it, vi } from "vitest";
import { Action } from "@/generated/browsergame/v1/game_pb";
import { shouldOpenChat } from "@/features/game/chat/chatLogic";
import { DEFAULT_BINDINGS, toProto, type Bindings, type Slot } from "@/features/game/input/bindings";
import { activeActions } from "@/features/game/input/input";
import {
  IDLE,
  captureKey,
  fill,
  keyBindingsStep,
  loadKeyLayout,
  type KeyBindingsState,
  type LayoutSource,
} from "./keyBindingsLogic";

const { MOVE_UP: UP, MOVE_DOWN: DOWN, MOVE_LEFT: LEFT, MOVE_RIGHT: RIGHT } = Action;

const withUp = (...codes: string[]): Bindings => new Map(DEFAULT_BINDINGS).set(UP, codes);
const rec = (action: Action, slot: Slot): KeyBindingsState => ({ mode: "recording", action, slot });
const press = (bindings: Bindings, action: Action, slot: Slot, code: string) =>
  keyBindingsStep(rec(action, slot), bindings, { type: "key", code });
const triggers = (code: string, bindings: Bindings | null, action: Action) =>
  bindings !== null && activeActions(new Set([code]), bindings).has(action);

function keyEvent(over: Partial<{ key: string; code: string; repeat: boolean; tagName: string }> = {}) {
  const { tagName = "BUTTON", ...rest } = over;
  const e = {
    key: "w",
    code: "KeyW",
    repeat: false,
    target: { tagName } as unknown as EventTarget,
    isComposing: false,
    altKey: false,
    ctrlKey: false,
    metaKey: false,
    defaultPrevented: false,
    preventDefault: vi.fn(() => { e.defaultPrevented = true; }),
    stopImmediatePropagation: vi.fn(),
    ...rest,
  };
  return e;
}

describe("keyBindingsStep", () => {
  it("startet, bricht ab und wechselt die Aufnahme", () => {
    expect(keyBindingsStep(IDLE, DEFAULT_BINDINGS, { type: "record", action: UP, slot: 1 })).toEqual({ state: rec(UP, 1), save: null });
    expect(keyBindingsStep(rec(UP, 1), DEFAULT_BINDINGS, { type: "record", action: UP, slot: 1 })).toEqual({ state: IDLE, save: null });
    expect(keyBindingsStep(rec(UP, 1), DEFAULT_BINDINGS, { type: "record", action: DOWN, slot: 0 })).toEqual({ state: rec(DOWN, 0), save: null });
  });

  it("belegt einen zweiten Platz (AK-5, AK-6)", () => {
    const { state, save } = press(DEFAULT_BINDINGS, UP, 1, "ArrowUp");
    expect(state).toEqual(IDLE);
    expect(save?.get(UP)).toEqual(["KeyW", "ArrowUp"]);
    expect(triggers("ArrowUp", save, UP)).toBe(true);
    expect(triggers("KeyW", save, UP)).toBe(true);
  });

  it("ersetzt eine Taste (AK-5)", () => {
    const { save } = press(DEFAULT_BINDINGS, UP, 0, "KeyI");
    expect(save?.get(UP)).toEqual(["KeyI"]);
    expect(triggers("KeyW", save, UP)).toBe(false);
    expect(triggers("KeyI", save, UP)).toBe(true);
  });

  it("ändert nichts bei derselben Taste", () => {
    expect(press(DEFAULT_BINDINGS, UP, 1, "KeyW")).toEqual({ state: IDLE, save: null });
  });

  it("ignoriert Tasten ohne Aufnahme", () => {
    const step = keyBindingsStep(IDLE, DEFAULT_BINDINGS, { type: "key", code: "KeyI" });
    expect(step.state).toBe(IDLE);
    expect(step.save).toBeNull();
  });

  describe("Konflikt (AK-7)", () => {
    const bindings = withUp("KeyW", "ArrowUp");

    it("überschreibt sofort ohne Rückfrage", () => {
      const { state, save } = press(bindings, DOWN, 1, "KeyW");
      expect(state).toEqual({ mode: "idle", notice: { kind: "overwritten", code: "KeyW", from: UP, to: DOWN } });
      expect(save?.get(UP)).toEqual(["ArrowUp"]);
      expect(save?.get(DOWN)).toEqual(["KeyS", "KeyW"]);
      expect(triggers("KeyW", save, DOWN)).toBe(true);
      expect(triggers("KeyW", save, UP)).toBe(false);
    });
  });

  it("überschreibt auch die letzte Taste einer anderen Aktion (AK-7, AK-11)", () => {
    const { state, save } = press(DEFAULT_BINDINGS, DOWN, 1, "KeyW");
    expect(state).toEqual({ mode: "idle", notice: { kind: "overwritten", code: "KeyW", from: UP, to: DOWN } });
    expect(save?.get(UP)).toEqual([]);
    expect(save?.get(DOWN)).toEqual(["KeyS", "KeyW"]);
  });

  it("meldet ohne Überschreiben nichts (AK-5, AK-7)", () => {
    expect(press(DEFAULT_BINDINGS, UP, 1, "ArrowUp").state).toBe(IDLE);
    expect(press(withUp("KeyW", "ArrowUp"), UP, 0, "ArrowUp")).toEqual({ state: IDLE, save: null });
  });

  it("räumt die Mitteilung beim nächsten Ereignis weg (AK-7)", () => {
    const noticed: KeyBindingsState = { mode: "idle", notice: { kind: "overwritten", code: "KeyW", from: UP, to: DOWN } };
    expect(keyBindingsStep(noticed, DEFAULT_BINDINGS, { type: "record", action: UP, slot: 0 }).state).toEqual(rec(UP, 0));
    expect(keyBindingsStep(noticed, DEFAULT_BINDINGS, { type: "cancel" }).state).toBe(IDLE);
    expect(keyBindingsStep(noticed, DEFAULT_BINDINGS, { type: "clear", action: UP, slot: 0 }).state).toBe(IDLE);
    expect(keyBindingsStep(noticed, withUp("KeyI"), { type: "reset" }).state).toBe(IDLE);
  });

  it("ersetzt die Mitteilung bei der nächsten überschriebenen Taste (AK-7)", () => {
    const { state } = keyBindingsStep(rec(LEFT, 1), DEFAULT_BINDINGS, { type: "key", code: "KeyD" });
    expect(state).toEqual({ mode: "idle", notice: { kind: "overwritten", code: "KeyD", from: RIGHT, to: LEFT } });
  });

  it.each([
    ["Enter", "chat"],
    ["NumpadEnter", "chat"],
    ["Escape", "menu"],
  ])("lehnt %s ab (AK-8)", (code, reservedFor) => {
    expect(press(DEFAULT_BINDINGS, UP, 1, code)).toEqual({
      state: { mode: "idle", notice: { kind: "reserved", reservedFor } },
      save: null,
    });
  });

  it("leert einen Platz (AK-11)", () => {
    const { state, save } = keyBindingsStep(IDLE, withUp("KeyW", "ArrowUp"), { type: "clear", action: UP, slot: 0 });
    expect(state).toEqual(IDLE);
    expect(save?.get(UP)).toEqual(["ArrowUp"]);
    expect(triggers("KeyW", save, UP)).toBe(false);
  });

  it("leert auch die letzte Taste (AK-11)", () => {
    const { save } = keyBindingsStep(IDLE, DEFAULT_BINDINGS, { type: "clear", action: UP, slot: 0 });
    expect(save?.get(UP)).toEqual([]);
    expect(triggers("KeyW", save, UP)).toBe(false);
    expect(keyBindingsStep(IDLE, DEFAULT_BINDINGS, { type: "clear", action: UP, slot: 1 }).save).toBeNull();
  });

  it("stellt den Standard wieder her (AK-10)", () => {
    const { state, save } = keyBindingsStep(IDLE, withUp("KeyI"), { type: "reset" });
    expect(state).toEqual(IDLE);
    expect(save).toBe(DEFAULT_BINDINGS);
    expect(save && toProto(save).bindings).toHaveLength(0);
    expect(keyBindingsStep(IDLE, DEFAULT_BINDINGS, { type: "reset" }).save).toBeNull();
  });

  it("räumt den Hinweis beim nächsten Ereignis weg", () => {
    const noticed: KeyBindingsState = { mode: "idle", notice: { kind: "reserved", reservedFor: "menu" } };
    expect(keyBindingsStep(noticed, DEFAULT_BINDINGS, { type: "record", action: UP, slot: 0 }).state).toEqual(rec(UP, 0));
    expect(keyBindingsStep(noticed, DEFAULT_BINDINGS, { type: "clear", action: UP, slot: 0 }).state).toEqual(IDLE);
  });
});

describe("captureKey", () => {
  it("lässt Events im Leerlauf unberührt", () => {
    const e = keyEvent();
    expect(captureKey(e, "idle")).toBeNull();
    expect(e.preventDefault).not.toHaveBeenCalled();
    expect(e.stopImmediatePropagation).not.toHaveBeenCalled();
  });

  it("nimmt eine Taste auf und stoppt das Event", () => {
    const e = keyEvent();
    expect(captureKey(e, "recording")).toEqual({ type: "key", code: "KeyW" });
    expect(e.preventDefault).toHaveBeenCalled();
    expect(e.stopImmediatePropagation).toHaveBeenCalled();
  });

  it("öffnet beim Aufnehmen von Enter den Chat nicht (AK-8)", () => {
    const fresh = () => keyEvent({ key: "Enter", code: "Enter", tagName: "BODY" });
    expect(shouldOpenChat(fresh())).toBe(true);
    const e = fresh();
    expect(captureKey(e, "recording")).toEqual({ type: "key", code: "Enter" });
    expect(e.defaultPrevented).toBe(true);
    expect(shouldOpenChat(e)).toBe(false);
  });

  it("bricht bei Esc ab und stoppt das Event", () => {
    const e = keyEvent({ key: "Escape", code: "Escape" });
    expect(captureKey(e, "recording")).toEqual({ type: "cancel" });
    expect(e.stopImmediatePropagation).toHaveBeenCalled();
  });

  it("ignoriert Wiederholungen, schluckt sie aber", () => {
    for (const over of [{ key: "Escape", code: "Escape" }, {}]) {
      const e = keyEvent({ ...over, repeat: true });
      expect(captureKey(e, "recording")).toEqual({ type: "ignore" });
      expect(e.preventDefault).toHaveBeenCalled();
    }
  });

  it.each(["", "Unidentified"])("ignoriert den Code %j", (code) => {
    expect(captureKey(keyEvent({ code }), "recording")).toEqual({ type: "ignore" });
  });

  it.each(["Tab", "ShiftLeft", "ControlLeft", "AltLeft", "MetaLeft", "Space"])("nimmt %s als Taste auf", (code) => {
    expect(captureKey(keyEvent({ key: code, code }), "recording")).toEqual({ type: "key", code });
  });
});

describe("loadKeyLayout", () => {
  it("liefert ohne API undefined", async () => {
    expect(await loadKeyLayout({})).toBeUndefined();
    expect(await loadKeyLayout({ keyboard: {} })).toBeUndefined();
  });

  it("liefert die Layout-Map", async () => {
    const map = new Map([["KeyW", "z"]]);
    expect(await loadKeyLayout({ keyboard: { getLayoutMap: () => Promise.resolve(map) } })).toBe(map);
  });

  it("liefert bei Fehlern undefined", async () => {
    const rejecting: LayoutSource = { keyboard: { getLayoutMap: () => Promise.reject(new Error("denied")) } };
    const throwing: LayoutSource = { keyboard: { getLayoutMap: () => { throw new Error("denied"); } } };
    expect(await loadKeyLayout(rejecting)).toBeUndefined();
    expect(await loadKeyLayout(throwing)).toBeUndefined();
  });
});

describe("fill", () => {
  it("ersetzt Platzhalter", () => {
    expect(fill("{key}-{action}", { key: "A", action: "B" })).toBe("A-B");
  });

  it("lässt unbekannte Platzhalter und Text ohne Platzhalter stehen", () => {
    expect(fill("{key}-{other}", { key: "A" })).toBe("A-{other}");
    expect(fill("plain", { key: "A" })).toBe("plain");
  });
});
