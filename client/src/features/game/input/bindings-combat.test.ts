import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import { Action, Attack_Kind, KeyBindingsSchema } from "@/generated/browsergame/v1/game_pb";
import { ZERO } from "@/features/game/core/vector";
import { ACTIONS, DEFAULT_BINDINGS, assign, fromProto, isDefault, toProto } from "./bindings";
import { activeActions, attackKindFor, combinedDirection } from "./input";

const { MOVE_UP: UP, MOVE_DOWN: DOWN, MOVE_LEFT: LEFT, MOVE_RIGHT: RIGHT, ATTACK_MELEE: MELEE, ATTACK_RANGED: RANGED } = Action;

const proto = (...bindings: [Action, string[]][]) =>
  create(KeyBindingsSchema, { bindings: bindings.map(([action, codes]) => ({ action, codes })) });

describe("Angriffe in der Tastenbelegung (013 plan.md Entscheidung 8)", () => {
  it("Standard: Nahkampf auf 1, Fernkampf auf 2, zweiter Platz leer", () => {
    expect(DEFAULT_BINDINGS.get(MELEE)).toEqual(["Digit1"]);
    expect(DEFAULT_BINDINGS.get(RANGED)).toEqual(["Digit2"]);
  });

  it("ACTIONS führt die Angriffe nach den Bewegungen", () => {
    expect(ACTIONS.slice(-2)).toEqual([MELEE, RANGED]);
    expect(ACTIONS).toHaveLength(6);
  });

  it("ältere Belegung ohne Angriffe bekommt die Standardtasten", () => {
    const result = fromProto(proto([UP, ["ArrowUp"]], [DOWN, ["ArrowDown"]], [LEFT, ["ArrowLeft"]], [RIGHT, ["ArrowRight"]]));
    expect(result.get(MELEE)).toEqual(["Digit1"]);
    expect(result.get(RANGED)).toEqual(["Digit2"]);
  });

  it("ist 1 schon belegt, bleibt Nahkampf leer (002 AK-11)", () => {
    const result = fromProto(proto([UP, ["Digit1"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]], [RIGHT, ["KeyD"]]));
    expect(result.get(MELEE)).toEqual([]);
    expect(result.get(RANGED)).toEqual(["Digit2"]);
    expect(fromProto(toProto(result)).get(MELEE)).toEqual([]);
  });

  it("Standard wird weiter als leere Nachricht gesendet (002 AK-10)", () => {
    expect(toProto(DEFAULT_BINDINGS).bindings).toEqual([]);
    expect(isDefault(new Map(DEFAULT_BINDINGS).set(MELEE, ["KeyF"]))).toBe(false);
  });

  it("Angriffstasten lassen sich überschreiben (002 AK-7)", () => {
    const result = assign(DEFAULT_BINDINGS, UP, 1, "Digit1");
    if (result.kind !== "ok") throw new Error("erwartet ok");
    expect(result.overwritten).toBe(MELEE);
    expect(result.bindings.get(MELEE)).toEqual([]);
    expect(result.bindings.get(UP)).toEqual(["KeyW", "Digit1"]);
  });

  it("1 und 2 lösen Angriffe aus, bewegen aber nicht", () => {
    expect(attackKindFor("Digit1", DEFAULT_BINDINGS)).toBe(Attack_Kind.MELEE);
    expect(attackKindFor("Digit2", DEFAULT_BINDINGS)).toBe(Attack_Kind.RANGED);
    const actions = activeActions(new Set(["Digit1", "Digit2"]), DEFAULT_BINDINGS);
    expect(combinedDirection(actions, null, ZERO)).toEqual(ZERO);
  });
});
