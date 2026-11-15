import { describe, expect, it } from "vitest";
import { Action, Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { DEFAULT_BINDINGS } from "./bindings";
import { attackKindFor, combinedDirection, PointerHold } from "./input";

const withAttacks = new Map(DEFAULT_BINDINGS)
  .set(Action.ATTACK_MELEE, ["Digit1"])
  .set(Action.ATTACK_RANGED, ["Digit2", "KeyQ"]);
const ORIGIN = { x: 0, y: 0 };

describe("attackKindFor (plan.md Entscheidung 8)", () => {
  it("ordnet die Tasten ihrer Angriffsart zu", () => {
    expect(attackKindFor("Digit1", withAttacks)).toBe(Attack_Kind.MELEE);
    expect(attackKindFor("Digit2", withAttacks)).toBe(Attack_Kind.RANGED);
  });

  it("nutzt auch den zweiten Platz", () => {
    expect(attackKindFor("KeyQ", withAttacks)).toBe(Attack_Kind.RANGED);
  });

  it("ordnet Bewegungstasten keine Angriffsart zu", () => {
    expect(attackKindFor("KeyW", withAttacks)).toBeNull();
  });

  it("ordnet nicht belegten Tasten keine Angriffsart zu", () => {
    expect(attackKindFor("Digit3", withAttacks)).toBeNull();
  });

  it("kennt ohne Angriffsaktionen in der Belegung keine Angriffsart", () => {
    expect(attackKindFor("Digit1", new Map([[Action.MOVE_UP, ["KeyW"]]]))).toBeNull();
  });
});

describe("Laufrichtung ohne Angriffsaktionen", () => {
  it("lässt den Zeiger maßgeblich, wenn nur eine Angriffstaste gedrückt ist", () => {
    expect(combinedDirection(new Set([Action.ATTACK_MELEE]), { x: 100, y: 0 }, ORIGIN)).toEqual({ x: 1, y: 0 });
  });

  it("steht still bei Angriffstaste ohne Zeiger", () => {
    expect(combinedDirection(new Set([Action.ATTACK_RANGED]), null, ORIGIN)).toEqual(ORIGIN);
  });

  it("lässt der Tastatur Vorrang, wenn eine Bewegungstaste dabei ist", () => {
    const actions = new Set([Action.ATTACK_MELEE, Action.MOVE_LEFT]);
    expect(combinedDirection(actions, { x: 100, y: 0 }, ORIGIN)).toEqual({ x: -1, y: 0 });
  });
});

describe("PointerHold.accepts", () => {
  const sample = { pointerId: 1, isPrimary: true, button: 0, clientX: 0, clientY: 0 };

  it("nimmt den primären Zeiger mit Haupttaste an", () => {
    expect(new PointerHold().accepts(sample)).toBe(true);
  });

  it("lehnt nicht primäre Zeiger und andere Tasten ab", () => {
    const hold = new PointerHold();
    expect(hold.accepts({ ...sample, isPrimary: false })).toBe(false);
    expect(hold.accepts({ ...sample, button: 2 })).toBe(false);
  });

  it("lehnt ab, solange ein Zeiger hält, und nimmt nach dem Loslassen wieder an", () => {
    const hold = new PointerHold();
    hold.down(sample);
    expect(hold.accepts(sample)).toBe(false);
    hold.release(1);
    expect(hold.accepts(sample)).toBe(true);
  });
});
