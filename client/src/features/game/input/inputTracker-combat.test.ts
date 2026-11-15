import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InputTracker } from "./input";
import { DEFAULT_BINDINGS } from "./bindings";
import { Action, Attack_Kind } from "@/generated/browsergame/v1/game_pb";

const element = (props: Partial<HTMLElement>) => props as unknown as EventTarget;
const FIELD = element({ tagName: "INPUT" });
const BODY = element({ tagName: "BODY" });
const PLAYER = { x: 0, y: 0 };
const STILL = { x: 0, y: 0 };
const UP = { x: 0, y: -1 };
const TOWARDS_POINTER = { x: 1, y: 0 };

function fakeEvent(type: string, target: EventTarget | null, init: object = {}): Event {
  const e = Object.assign(new Event(type, { bubbles: true, cancelable: true }), init);
  Object.defineProperty(e, "target", { value: target });
  return e;
}

let fakeWindow: EventTarget;
let fakeDocument: EventTarget & { hidden: boolean; activeElement: unknown };
let canvas: EventTarget & { setPointerCapture: ReturnType<typeof vi.fn>; getBoundingClientRect: () => DOMRect };
let tracker: InputTracker;
let hit: boolean;
let pickTarget: ReturnType<typeof vi.fn<(x: number, y: number) => boolean>>;
let onAttack: ReturnType<typeof vi.fn<(kind: Attack_Kind) => void>>;

beforeEach(() => {
  fakeWindow = new EventTarget();
  fakeDocument = Object.assign(new EventTarget(), { hidden: false, activeElement: BODY });
  vi.stubGlobal("window", fakeWindow);
  vi.stubGlobal("document", fakeDocument);
  canvas = Object.assign(new EventTarget(), { setPointerCapture: vi.fn(), getBoundingClientRect: () => ({}) as DOMRect });
  hit = false;
  pickTarget = vi.fn((_x: number, _y: number) => hit);
  onAttack = vi.fn();
  tracker = new InputTracker(canvas as unknown as HTMLCanvasElement, { pickTarget, onAttack });
  tracker.setBindings(new Map(DEFAULT_BINDINGS).set(Action.ATTACK_MELEE, ["Digit1"]).set(Action.ATTACK_RANGED, ["Digit2"]));
});

afterEach(() => {
  tracker.dispose();
  vi.unstubAllGlobals();
});

const direction = () => tracker.direction(PLAYER, (x, y) => ({ x, y }));
const keydown = (code: string, target: EventTarget, repeat = false) => {
  const e = fakeEvent("keydown", target, { code, repeat });
  fakeWindow.dispatchEvent(e);
  return e;
};
const keyup = (code: string, target: EventTarget) => { fakeWindow.dispatchEvent(fakeEvent("keyup", target, { code })); };
const pointerdown = (init: object = {}) => {
  const e = fakeEvent("pointerdown", canvas, { pointerId: 1, isPrimary: true, button: 0, clientX: 100, clientY: 0, ...init });
  canvas.dispatchEvent(e);
  return e;
};
const pointerup = (pointerId = 1) => { canvas.dispatchEvent(fakeEvent("pointerup", canvas, { pointerId })); };

describe("Zielauswahl per Zeiger (AK-1, plan.md Entscheidung 7)", () => {
  it("wählt ein getroffenes Ziel aus, ohne zu laufen", () => {
    hit = true;
    const e = pointerdown();
    expect(pickTarget).toHaveBeenCalledWith(100, 0);
    expect(direction()).toEqual(STILL);
    expect(canvas.setPointerCapture).not.toHaveBeenCalled();
    expect(e.defaultPrevented).toBe(true);
  });

  it("lässt nach einer Auswahl das nächste Laufen zu", () => {
    hit = true;
    pointerdown();
    pointerup();
    hit = false;
    pointerdown();
    expect(direction()).toEqual(TOWARDS_POINTER);
    expect(canvas.setPointerCapture).toHaveBeenCalledTimes(1);
  });

  it("hält ohne Ziel wie bisher", () => {
    const e = pointerdown();
    expect(direction()).toEqual(TOWARDS_POINTER);
    expect(canvas.setPointerCapture).toHaveBeenCalledWith(1);
    expect(e.defaultPrevented).toBe(true);
  });

  it("nimmt dem Eingabefeld bei getroffenem Ziel den Fokus (011 AK-6)", () => {
    const blur = vi.fn();
    fakeDocument.activeElement = { tagName: "INPUT", blur };
    hit = true;
    pointerdown();
    expect(blur).toHaveBeenCalledTimes(1);
  });

  it("fragt bei Rechtsklick und nicht primärem Zeiger nicht nach einem Ziel", () => {
    pointerdown({ button: 2 });
    pointerdown({ isPrimary: false, pointerId: 2 });
    expect(pickTarget).not.toHaveBeenCalled();
  });

  it("fragt bei offenem Menü nicht nach einem Ziel", () => {
    tracker.setSuspended(true);
    const e = pointerdown();
    expect(pickTarget).not.toHaveBeenCalled();
    expect(e.defaultPrevented).toBe(false);
  });

  it("fragt während eines haltenden Zeigers nicht erneut", () => {
    pointerdown();
    pointerdown({ pointerId: 2 });
    expect(pickTarget).toHaveBeenCalledTimes(1);
  });

  it("hält auch ohne Callbacks", () => {
    const plain = new InputTracker(canvas as unknown as HTMLCanvasElement);
    pointerdown();
    expect(plain.direction(PLAYER, (x, y) => ({ x, y }))).toEqual(TOWARDS_POINTER);
    plain.dispose();
  });
});

describe("Angriffstasten (plan.md Entscheidung 8)", () => {
  it("meldet die Angriffsart der Taste", () => {
    keydown("Digit1", BODY);
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.MELEE);
    keydown("Digit2", BODY);
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.RANGED);
  });

  it("löst bei Tastenwiederholung nicht erneut aus", () => {
    keydown("Digit1", BODY);
    for (let i = 0; i < 3; i++) keydown("Digit1", BODY, true);
    expect(onAttack).toHaveBeenCalledTimes(1);
  });

  it("löst nach dem Loslassen erneut aus", () => {
    keydown("Digit1", BODY);
    keyup("Digit1", BODY);
    keydown("Digit1", BODY);
    expect(onAttack).toHaveBeenCalledTimes(2);
  });

  it("löst im Eingabefeld nicht aus", () => {
    keydown("Digit1", FIELD);
    expect(onAttack).not.toHaveBeenCalled();
  });

  it("löst bei offenem Menü nicht aus", () => {
    tracker.setSuspended(true);
    keydown("Digit1", BODY);
    expect(onAttack).not.toHaveBeenCalled();
  });

  it("löst bei Bewegungstasten nicht aus", () => {
    keydown("KeyW", BODY);
    expect(onAttack).not.toHaveBeenCalled();
  });

  it("löst bei nicht belegten Tasten nicht aus", () => {
    keydown("Digit3", BODY);
    tracker.setBindings(new Map([[Action.MOVE_UP, ["KeyW"]]]));
    keydown("Digit1", BODY);
    expect(onAttack).not.toHaveBeenCalled();
  });

  it("nutzt eine neue Belegung sofort", () => {
    tracker.setBindings(new Map([[Action.ATTACK_MELEE, ["KeyF"]]]));
    keydown("KeyF", BODY);
    expect(onAttack).toHaveBeenCalledTimes(1);
    expect(onAttack).toHaveBeenLastCalledWith(Attack_Kind.MELEE);
    keydown("Digit1", BODY);
    expect(onAttack).toHaveBeenCalledTimes(1);
  });

  it("löst nach dispose nicht mehr aus", () => {
    tracker.dispose();
    keydown("Digit1", BODY);
    expect(onAttack).not.toHaveBeenCalled();
  });
});

describe("Angriffstaste und Laufen", () => {
  it("lässt einen haltenden Zeiger weiterlaufen", () => {
    pointerdown();
    keydown("Digit1", BODY);
    expect(direction()).toEqual(TOWARDS_POINTER);
  });

  it("ändert die Tastaturrichtung nicht", () => {
    keydown("KeyW", BODY);
    keydown("Digit1", BODY);
    expect(direction()).toEqual(UP);
  });
});
