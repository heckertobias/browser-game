import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InputTracker, isEditableTarget } from "./input";
import { DEFAULT_BINDINGS } from "./bindings";
import { Action } from "@/generated/browsergame/v1/game_pb";

const element = (props: Partial<HTMLElement>) => props as unknown as EventTarget;
const FIELD = element({ tagName: "INPUT" });
const BODY = element({ tagName: "BODY" });
const BUTTON = element({ tagName: "BUTTON" });
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

beforeEach(() => {
  fakeWindow = new EventTarget();
  fakeDocument = Object.assign(new EventTarget(), { hidden: false, activeElement: BODY });
  vi.stubGlobal("window", fakeWindow);
  vi.stubGlobal("document", fakeDocument);
  canvas = Object.assign(new EventTarget(), { setPointerCapture: vi.fn(), getBoundingClientRect: () => ({}) as DOMRect });
  tracker = new InputTracker(canvas as unknown as HTMLCanvasElement);
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
const focusin = (target: EventTarget) => { fakeWindow.dispatchEvent(fakeEvent("focusin", target)); };
const fakeFieldFocus = (blur: () => void) => { fakeDocument.activeElement = { tagName: "INPUT", blur }; };
const pointerdown = (button = 0) => {
  const e = fakeEvent("pointerdown", canvas, { pointerId: 1, isPrimary: true, button, clientX: 100, clientY: 0 });
  canvas.dispatchEvent(e);
  return e;
};

describe("isEditableTarget (011 AK-6)", () => {
  it.each(["INPUT", "TEXTAREA", "SELECT"])("erkennt %s als Eingabefeld", (tagName) => {
    expect(isEditableTarget(element({ tagName }))).toBe(true);
  });

  it("erkennt contenteditable als Eingabefeld", () => {
    expect(isEditableTarget(element({ tagName: "DIV", isContentEditable: true }))).toBe(true);
  });

  it.each(["BODY", "BUTTON", "CANVAS", "DIV"])("erkennt %s nicht als Eingabefeld", (tagName) => {
    expect(isEditableTarget(element({ tagName, isContentEditable: false }))).toBe(false);
  });

  it("erkennt kein Element nicht als Eingabefeld", () => {
    expect(isEditableTarget(null)).toBe(false);
    expect(isEditableTarget(new EventTarget())).toBe(false);
  });
});

describe("InputTracker beim Tippen (011 AK-6)", () => {
  it("steuert mit Tasten außerhalb eines Feldes", () => {
    expect(keydown("KeyW", BODY).defaultPrevented).toBe(false);
    expect(direction()).toEqual(UP);
  });

  it.each(["KeyW", "KeyA", "KeyS", "KeyD"])("ignoriert %s im Feld", (code) => {
    keydown(code, FIELD);
    expect(direction()).toEqual(STILL);
  });

  it.each(["KeyW", "KeyA", "KeyS", "KeyD", "Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Enter", "Escape"])(
    "blockiert %s im Feld nicht",
    (code) => {
      expect(keydown(code, FIELD).defaultPrevented).toBe(false);
    },
  );

  it("stoppt die Figur, wenn das Feld den Fokus bekommt", () => {
    keydown("KeyW", BODY);
    focusin(FIELD);
    expect(direction()).toEqual(STILL);
  });

  it("nimmt Tasten nach keyup im Feld wieder an", () => {
    keydown("KeyW", BODY);
    focusin(FIELD);
    keyup("KeyW", FIELD);
    expect(direction()).toEqual(STILL);
    keydown("KeyW", BODY);
    expect(direction()).toEqual(UP);
  });

  it("filtert keyup im Feld nie", () => {
    keydown("KeyW", BODY);
    keyup("KeyW", FIELD);
    expect(direction()).toEqual(STILL);
  });

  it("stoppt nicht, wenn ein anderes Element den Fokus bekommt", () => {
    keydown("KeyW", BODY);
    focusin(BUTTON);
    expect(direction()).toEqual(UP);
  });

  it("läuft nicht los, wenn im Feld gedrückt und danach ohne Wiederholung losgelassen wird", () => {
    keydown("KeyW", FIELD);
    expect(direction()).toEqual(STILL);
    keyup("KeyW", BODY);
    expect(direction()).toEqual(STILL);
  });

  it("hängt nicht, wenn die Wiederholung nach Fokusverlust ankommt", () => {
    keydown("KeyW", FIELD);
    expect(direction()).toEqual(STILL);
    keydown("KeyW", BODY, true);
    expect(direction()).toEqual(UP);
    keyup("KeyW", BODY);
    expect(direction()).toEqual(STILL);
  });

  it("lässt einen gehaltenen Zeiger beim Fokus aufs Feld weiterlaufen", () => {
    pointerdown();
    focusin(FIELD);
    expect(direction()).toEqual(TOWARDS_POINTER);
  });

  it("übergibt nach dem Fokus die Steuerung vom Tastendruck an den Zeiger", () => {
    pointerdown();
    keydown("KeyW", BODY);
    expect(direction()).toEqual(UP);
    focusin(FIELD);
    expect(direction()).toEqual(TOWARDS_POINTER);
  });

  it("hängt den focusin-Listener ans signal", () => {
    keydown("KeyW", BODY);
    tracker.dispose();
    focusin(FIELD);
    expect(direction()).toEqual(UP);
  });

  it("hängt keydown und pointerdown ans signal", () => {
    const blur = vi.fn();
    fakeFieldFocus(blur);
    tracker.dispose();
    keydown("KeyW", BODY);
    pointerdown();
    expect(direction()).toEqual(STILL);
    expect(blur).not.toHaveBeenCalled();
  });
});

describe("Canvas-Druck bei fokussiertem Feld (011 AK-6)", () => {
  it("nimmt dem Feld den Fokus und startet das Halten", () => {
    const blur = vi.fn();
    fakeDocument.activeElement = { tagName: "INPUT", blur };
    const e = pointerdown();
    expect(blur).toHaveBeenCalledTimes(1);
    expect(canvas.setPointerCapture).toHaveBeenCalled();
    expect(e.defaultPrevented).toBe(true);
    expect(direction()).toEqual(TOWARDS_POINTER);
  });

  it("lässt andere fokussierte Elemente unberührt", () => {
    const blur = vi.fn();
    fakeDocument.activeElement = { tagName: "BUTTON", blur };
    pointerdown();
    expect(blur).not.toHaveBeenCalled();
    expect(direction()).toEqual(TOWARDS_POINTER);
  });

  it("nimmt dem Feld bei einem Druck, der nicht hält, den Fokus nicht", () => {
    const blur = vi.fn();
    fakeDocument.activeElement = { tagName: "INPUT", blur };
    pointerdown(2);
    expect(blur).not.toHaveBeenCalled();
    expect(direction()).toEqual(STILL);
  });
});

describe("InputTracker: Fenster und Tab bleiben (007)", () => {
  it("setzt Tasten und Zeiger zurück, wenn das Fenster den Fokus verliert", () => {
    keydown("KeyW", BODY);
    pointerdown();
    fakeWindow.dispatchEvent(new Event("blur"));
    expect(direction()).toEqual(STILL);
  });

  it("setzt Tasten und Zeiger zurück, wenn der Tab versteckt wird", () => {
    keydown("KeyW", BODY);
    pointerdown();
    fakeDocument.hidden = true;
    fakeDocument.dispatchEvent(new Event("visibilitychange"));
    expect(direction()).toEqual(STILL);
  });

  it("setzt nichts zurück, wenn der Tab sichtbar bleibt", () => {
    keydown("KeyW", BODY);
    fakeDocument.dispatchEvent(new Event("visibilitychange"));
    expect(direction()).toEqual(UP);
  });
});

describe("InputTracker: Belegung (002 AK-5, AK-6)", () => {
  const upOn = (...codes: string[]) => new Map(DEFAULT_BINDINGS).set(Action.MOVE_UP, codes);

  it("steuert mit der neuen Taste, die alte wirkt nicht mehr", () => {
    tracker.setBindings(upOn("ArrowUp"));
    keydown("KeyW", BODY);
    expect(direction()).toEqual(STILL);
    keyup("KeyW", BODY);
    keydown("ArrowUp", BODY);
    expect(direction()).toEqual(UP);
  });

  it("wirkt sofort, auch für eine schon gedrückte Taste", () => {
    keydown("ArrowUp", BODY);
    expect(direction()).toEqual(STILL);
    tracker.setBindings(upOn("ArrowUp"));
    expect(direction()).toEqual(UP);
  });

  it.each(["KeyW", "ArrowUp"])("beide Tasten einer Aktion steuern: %s", (code) => {
    tracker.setBindings(upOn("KeyW", "ArrowUp"));
    keydown(code, BODY);
    expect(direction()).toEqual(UP);
  });
});

describe("InputTracker: Menü offen (002 AK-4)", () => {
  it("hält die Figur an, obwohl eine Taste gedrückt ist", () => {
    keydown("KeyW", BODY);
    tracker.setSuspended(true);
    expect(direction()).toEqual(STILL);
  });

  it("hält die Figur an, obwohl der Zeiger gehalten wird", () => {
    pointerdown();
    tracker.setSuspended(true);
    expect(direction()).toEqual(STILL);
  });

  it("ignoriert Tasten, die bei offenem Menü gedrückt werden, auch nach dem Schließen", () => {
    tracker.setSuspended(true);
    keydown("KeyW", BODY);
    expect(direction()).toEqual(STILL);
    tracker.setSuspended(false);
    expect(direction()).toEqual(STILL);
  });

  it("startet bei offenem Menü kein Halten und nimmt keinem Feld den Fokus", () => {
    const blur = vi.fn();
    fakeFieldFocus(blur);
    tracker.setSuspended(true);
    const e = pointerdown();
    expect(e.defaultPrevented).toBe(false);
    expect(canvas.setPointerCapture).not.toHaveBeenCalled();
    expect(blur).not.toHaveBeenCalled();
    tracker.setSuspended(false);
    expect(direction()).toEqual(STILL);
  });

  it("löscht beim Öffnen gedrückte Tasten und gehaltenen Zeiger", () => {
    keydown("KeyW", BODY);
    pointerdown();
    tracker.setSuspended(true);
    tracker.setSuspended(false);
    expect(direction()).toEqual(STILL);
  });

  it("nimmt eine über das Schließen gehaltene Taste mit ihrer Wiederholung wieder an", () => {
    keydown("KeyW", BODY);
    tracker.setSuspended(true);
    tracker.setSuspended(false);
    keydown("KeyW", BODY, true);
    expect(direction()).toEqual(UP);
  });

  it("steuert nach dem Schließen wieder mit Tasten und Zeiger", () => {
    tracker.setSuspended(true);
    tracker.setSuspended(false);
    keydown("KeyW", BODY);
    expect(direction()).toEqual(UP);
    keyup("KeyW", BODY);
    pointerdown();
    expect(direction()).toEqual(TOWARDS_POINTER);
  });
});
