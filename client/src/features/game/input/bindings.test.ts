import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import { Action, KeyBindingsSchema, type KeyBindings } from "@/generated/browsergame/v1/game_pb";
import {
  ACTIONS,
  DEFAULT_BINDINGS,
  RESERVED_CODES,
  assign,
  clear,
  fromProto,
  isDefault,
  keyLabel,
  toProto,
  type Bindings,
} from "./bindings";

const { MOVE_UP: UP, MOVE_DOWN: DOWN, MOVE_LEFT: LEFT, MOVE_RIGHT: RIGHT } = Action;

const proto = (...bindings: [Action, string[]][]) =>
  create(KeyBindingsSchema, { bindings: bindings.map(([action, codes]) => ({ action, codes })) });

const withUp = (...codes: string[]): Bindings => new Map(DEFAULT_BINDINGS).set(UP, codes);

/** Spiegelt KeyBindingRules.IsValid (Server): Der Client darf nie etwas erzeugen, das der Server verwirft. */
function serverAccepts(kb: KeyBindings): boolean {
  if (kb.bindings.length === 0) {
    return true;
  }
  const known = [
    Action.MOVE_UP,
    Action.MOVE_DOWN,
    Action.MOVE_LEFT,
    Action.MOVE_RIGHT,
    Action.ATTACK_MELEE,
    Action.ATTACK_RANGED,
  ];
  const reserved = ["Escape", "Enter", "NumpadEnter"];
  const actions = new Set<Action>();
  const codes = new Set<string>();
  for (const binding of kb.bindings) {
    if (!known.includes(binding.action) || actions.has(binding.action)) {
      return false;
    }
    actions.add(binding.action);
    if (binding.codes.length > 2) {
      return false;
    }
    for (const code of binding.codes) {
      if (!/^[A-Za-z0-9]{1,32}$/.test(code) || reserved.includes(code) || codes.has(code)) {
        return false;
      }
      codes.add(code);
    }
  }
  return true;
}

describe("Standard (002 AK-10)", () => {
  it("DEFAULT_BINDINGS ist WASD, Angriffe auf 1 und 2, zweiter Platz leer", () => {
    expect([...DEFAULT_BINDINGS]).toEqual([
      [UP, ["KeyW"]],
      [DOWN, ["KeyS"]],
      [LEFT, ["KeyA"]],
      [RIGHT, ["KeyD"]],
      [Action.ATTACK_MELEE, ["Digit1"]],
      [Action.ATTACK_RANGED, ["Digit2"]],
    ]);
  });

  it("ACTIONS enthält jede Aktion in Anzeigereihenfolge", () => {
    expect(ACTIONS).toEqual([UP, DOWN, LEFT, RIGHT, Action.ATTACK_MELEE, Action.ATTACK_RANGED]);
  });

  it("RESERVED_CODES entspricht dem Server (002 AK-8)", () => {
    expect(RESERVED_CODES).toEqual(["Escape", "Enter", "NumpadEnter"]);
  });
});

describe("toProto und isDefault (002 AK-10)", () => {
  it("die Standardbelegung wird als leere Nachricht gesendet", () => {
    expect(isDefault(DEFAULT_BINDINGS)).toBe(true);
    expect(toProto(DEFAULT_BINDINGS).bindings).toEqual([]);
  });

  it("isDefault vergleicht Werte, nicht Identität", () => {
    const same: Bindings = new Map([
      [UP, ["KeyW"]],
      [DOWN, ["KeyS"]],
      [LEFT, ["KeyA"]],
      [RIGHT, ["KeyD"]],
      [Action.ATTACK_MELEE, ["Digit1"]],
      [Action.ATTACK_RANGED, ["Digit2"]],
    ]);
    expect(isDefault(same)).toBe(true);
    expect(toProto(same).bindings).toEqual([]);
  });

  it("eine geänderte Belegung sendet alle Aktionen in Reihenfolge", () => {
    const b = withUp("ArrowUp");
    expect(isDefault(b)).toBe(false);
    const kb = toProto(b);
    expect(kb.bindings.map((x) => x.action)).toEqual(ACTIONS);
    expect(kb.bindings[0]?.codes).toEqual(["ArrowUp"]);
    expect(serverAccepts(kb)).toBe(true);
  });

  it("zwei Tasten bleiben erhalten (002 AK-6)", () => {
    expect(toProto(withUp("KeyW", "ArrowUp")).bindings[0]?.codes).toEqual(["KeyW", "ArrowUp"]);
  });

  it("Rundreise ergibt die Ausgangsbelegung", () => {
    const b = withUp("KeyW", "ArrowUp");
    expect([...fromProto(toProto(b))]).toEqual([...b]);
  });
});

describe("fromProto", () => {
  it("fehlende oder leere Nachricht ergibt den Standard (002 AK-10)", () => {
    expect(fromProto(undefined)).toBe(DEFAULT_BINDINGS);
    expect(fromProto(create(KeyBindingsSchema))).toBe(DEFAULT_BINDINGS);
  });

  it("übernimmt eine gültige Belegung in ACTIONS-Reihenfolge", () => {
    const result = fromProto(
      proto([RIGHT, ["ArrowRight"]], [LEFT, ["ArrowLeft"]], [DOWN, ["ArrowDown"]], [UP, ["ArrowUp", "KeyW"]]),
    );
    expect([...result]).toEqual([
      [UP, ["ArrowUp", "KeyW"]],
      [DOWN, ["ArrowDown"]],
      [LEFT, ["ArrowLeft"]],
      [RIGHT, ["ArrowRight"]],
      [Action.ATTACK_MELEE, ["Digit1"]],
      [Action.ATTACK_RANGED, ["Digit2"]],
    ]);
  });

  it("eine fehlende Aktion bekommt ihren freien Standard", () => {
    const result = fromProto(proto([UP, ["ArrowUp"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]]));
    expect(result.get(RIGHT)).toEqual(["KeyD"]);
  });

  it("eine fehlende Aktion mit belegtem Standard bleibt leer (AK-11)", () => {
    const result = fromProto(proto([UP, ["KeyD"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]]));
    expect([...result]).toEqual([
      [UP, ["KeyD"]],
      [DOWN, ["KeyS"]],
      [LEFT, ["KeyA"]],
      [RIGHT, []],
      [Action.ATTACK_MELEE, ["Digit1"]],
      [Action.ATTACK_RANGED, ["Digit2"]],
    ]);
    expect(serverAccepts(toProto(result))).toBe(true);
  });

  it("bereinigt kaputte Daten (002 AK-7, AK-8)", () => {
    const result = fromProto(
      proto(
        [Action.UNSPECIFIED, ["KeyQ"]],
        [99 as unknown as Action, ["KeyE"]],
        [UP, ["Escape", "", "Key-W", "A".repeat(33), "ArrowUp"]],
        [UP, ["KeyZ"]],
        [DOWN, ["ArrowUp", "KeyS"]],
        [LEFT, ["KeyA", "KeyJ", "KeyK"]],
        [RIGHT, ["Enter", "NumpadEnter", "KeyD"]],
      ),
    );
    expect([...result]).toEqual([
      [UP, ["ArrowUp"]],
      [DOWN, ["KeyS"]],
      [LEFT, ["KeyA", "KeyJ"]],
      [RIGHT, ["KeyD"]],
      [Action.ATTACK_MELEE, ["Digit1"]],
      [Action.ATTACK_RANGED, ["Digit2"]],
    ]);
    expect(serverAccepts(toProto(result))).toBe(true);
  });

  it("eine mitgeschickte Aktion mit nur ungültigen Codes bleibt leer (AK-11)", () => {
    const result = fromProto(proto([UP, ["Enter"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]], [RIGHT, ["KeyD"]]));
    expect(result.get(UP)).toEqual([]);
  });
});

describe("assign (002 AK-5, AK-7, AK-8)", () => {
  it("eine freie Taste ersetzt Platz 0 (AK-5)", () => {
    const result = assign(DEFAULT_BINDINGS, UP, 0, "ArrowUp");
    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") {
      return;
    }
    expect(result.bindings.get(UP)).toEqual(["ArrowUp"]);
    expect(result.bindings.get(DOWN)).toEqual(["KeyS"]);
    expect(DEFAULT_BINDINGS.get(UP)).toEqual(["KeyW"]);
  });

  it("eine freie Taste auf leerem Platz 1 wird angehängt (AK-5, AK-6)", () => {
    const result = assign(DEFAULT_BINDINGS, UP, 1, "ArrowUp");
    expect(result.kind === "ok" && result.bindings.get(UP)).toEqual(["KeyW", "ArrowUp"]);
  });

  it("Platz 1 bei zwei Tasten ersetzt den zweiten", () => {
    const result = assign(withUp("KeyW", "ArrowUp"), UP, 1, "KeyI");
    expect(result.kind === "ok" && result.bindings.get(UP)).toEqual(["KeyW", "KeyI"]);
  });

  it("dieselbe Taste bei derselben Aktion wird ignoriert", () => {
    const b = withUp("KeyW", "ArrowUp");
    for (const code of ["ArrowUp", "KeyW"]) {
      const result = assign(b, UP, 0, code);
      expect(result.kind).toBe("ok");
      expect(result.kind === "ok" && result.bindings).toBe(b);
    }
  });

  it("eine freie Taste meldet nichts als überschrieben (AK-5)", () => {
    for (const result of [
      assign(DEFAULT_BINDINGS, UP, 0, "ArrowUp"),
      assign(withUp("KeyW", "ArrowUp"), UP, 0, "KeyW"),
    ]) {
      expect(result.kind).toBe("ok");
      expect(result).not.toHaveProperty("overwritten");
    }
  });

  it("eine Taste einer anderen Aktion wird sofort überschrieben, der zweite Platz rückt nach (AK-7)", () => {
    const b = withUp("KeyW", "ArrowUp");
    const result = assign(b, DOWN, 0, "KeyW");
    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") {
      return;
    }
    expect(result.overwritten).toBe(UP);
    expect(result.bindings.get(UP)).toEqual(["ArrowUp"]);
    expect(result.bindings.get(DOWN)).toEqual(["KeyW"]);
    expect(b.get(UP)).toEqual(["KeyW", "ArrowUp"]);
    expect(serverAccepts(toProto(result.bindings))).toBe(true);
  });

  it("überschreibt auf leerem Platz 1 (AK-6, AK-7)", () => {
    const result = assign(withUp("KeyW", "ArrowUp"), DOWN, 1, "ArrowUp");
    expect(result.kind === "ok" && result.overwritten).toBe(UP);
    expect(result.kind === "ok" && result.bindings.get(UP)).toEqual(["KeyW"]);
    expect(result.kind === "ok" && result.bindings.get(DOWN)).toEqual(["KeyS", "ArrowUp"]);
  });

  it("die andere Aktion darf dabei ohne Taste sein (AK-7, AK-11)", () => {
    const result = assign(DEFAULT_BINDINGS, DOWN, 1, "KeyW");
    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") {
      return;
    }
    expect(result.overwritten).toBe(UP);
    expect(result.bindings.get(UP)).toEqual([]);
    expect(result.bindings.get(DOWN)).toEqual(["KeyS", "KeyW"]);
    expect(serverAccepts(toProto(result.bindings))).toBe(true);
    expect(fromProto(toProto(result.bindings)).get(UP)).toEqual([]);
  });

  it("Ersetzen auf Platz 0 gibt die alte Taste frei (AK-7)", () => {
    const result = assign(DEFAULT_BINDINGS, UP, 0, "KeyS");
    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") {
      return;
    }
    expect(result.overwritten).toBe(DOWN);
    expect(result.bindings.get(UP)).toEqual(["KeyS"]);
    expect(result.bindings.get(DOWN)).toEqual([]);
    expect([...result.bindings.values()].flat()).not.toContain("KeyW");
  });

  it.each([
    ["Escape", "menu"],
    ["Enter", "chat"],
    ["NumpadEnter", "chat"],
  ])("%s ist reserviert (AK-8)", (code, reservedFor) => {
    expect(assign(DEFAULT_BINDINGS, UP, 1, code)).toEqual({ kind: "reserved", reservedFor });
  });

  it("jeder Code aus RESERVED_CODES ist reserviert (AK-8)", () => {
    for (const code of RESERVED_CODES) {
      expect(assign(DEFAULT_BINDINGS, UP, 1, code).kind).toBe("reserved");
    }
  });

  it.each(["", "Key-W", "A".repeat(33)])("ungültiger Code %j wird ignoriert", (code) => {
    const result = assign(DEFAULT_BINDINGS, UP, 1, code);
    expect(result.kind).toBe("ok");
    expect(result.kind === "ok" && result.bindings).toBe(DEFAULT_BINDINGS);
  });

  it("eine unbekannte Aktion wird ignoriert", () => {
    const result = assign(DEFAULT_BINDINGS, Action.UNSPECIFIED, 0, "KeyQ");
    expect(result.kind).toBe("ok");
    expect(result.kind === "ok" && result.bindings).toBe(DEFAULT_BINDINGS);
  });
});

describe("clear (002 AK-11)", () => {
  it("auch die letzte Taste ist leerbar", () => {
    const result = clear(DEFAULT_BINDINGS, UP, 0);
    expect(result.get(UP)).toEqual([]);
    expect(result.get(DOWN)).toEqual(["KeyS"]);
    expect(DEFAULT_BINDINGS.get(UP)).toEqual(["KeyW"]);
    expect(serverAccepts(toProto(result))).toBe(true);
  });

  it("ein leerer Platz ändert nichts", () => {
    expect(clear(DEFAULT_BINDINGS, UP, 1)).toBe(DEFAULT_BINDINGS);
    const empty = withUp();
    expect(clear(empty, UP, 0)).toBe(empty);
  });

  it("löschen lässt die andere Taste nachrücken", () => {
    const b = withUp("KeyW", "ArrowUp");
    const first = clear(b, UP, 0);
    const second = clear(b, UP, 1);
    expect(first.get(UP)).toEqual(["ArrowUp"]);
    expect(second.get(UP)).toEqual(["KeyW"]);
    expect(b.get(UP)).toEqual(["KeyW", "ArrowUp"]);
    expect(serverAccepts(toProto(first))).toBe(true);
    expect(serverAccepts(toProto(second))).toBe(true);
  });
});

describe("leere Aktionen (002 AK-11)", () => {
  it("toProto schickt eine leere Aktion mit", () => {
    const kb = toProto(withUp());
    expect(kb.bindings.map((x) => x.action)).toEqual(ACTIONS);
    expect(kb.bindings[0]?.codes).toEqual([]);
    expect(serverAccepts(kb)).toBe(true);
  });

  it("eine mitgeschickte leere Aktion bekommt nicht ihren Standard", () => {
    const result = fromProto(proto([UP, []], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]], [RIGHT, ["KeyD"]]));
    expect(result.get(UP)).toEqual([]);
  });

  it("Rundreise ohne jede Taste bleibt leer", () => {
    const none: Bindings = new Map<Action, readonly string[]>(ACTIONS.map((a) => [a, []]));
    expect(isDefault(none)).toBe(false);
    expect(serverAccepts(toProto(none))).toBe(true);
    expect([...fromProto(toProto(none))]).toEqual([...none]);
  });
});

describe("keyLabel", () => {
  it.each([
    ["KeyW", "W"],
    ["Digit1", "1"],
    ["ArrowUp", "↑"],
    ["ArrowDown", "↓"],
    ["ArrowLeft", "←"],
    ["ArrowRight", "→"],
    ["Space", "Space"],
    ["Numpad1", "Numpad1"],
  ])("ohne Layout wird %s zu %s", (code, label) => {
    expect(keyLabel(code)).toBe(label);
  });

  it("die Layout-Map hat Vorrang", () => {
    expect(keyLabel("KeyY", new Map([["KeyY", "z"]]))).toBe("Z");
  });

  it("fehlt der Code in der Map oder ist der Wert leer, gilt der Name aus dem Code", () => {
    expect(keyLabel("ArrowUp", new Map())).toBe("↑");
    expect(keyLabel("KeyW", new Map([["KeyW", ""]]))).toBe("W");
  });

  it("Großschreibung nur ohne Längenänderung", () => {
    expect(keyLabel("Minus", new Map([["Minus", "ß"]]))).toBe("ß");
  });
});
