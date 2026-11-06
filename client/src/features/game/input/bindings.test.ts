import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import { Action, KeyBindingsSchema, type KeyBindings } from "@/generated/browsergame/v1/game_pb";
import {
  ACTIONS,
  DEFAULT_BINDINGS,
  MOVE_ACTIONS,
  RESERVED_CODES,
  assign,
  assignOverwriting,
  canClear,
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
  const moves = [Action.MOVE_UP, Action.MOVE_DOWN, Action.MOVE_LEFT, Action.MOVE_RIGHT];
  const reserved = ["Escape", "Enter", "NumpadEnter"];
  const actions = new Set<Action>();
  const codes = new Set<string>();
  for (const binding of kb.bindings) {
    if (!moves.includes(binding.action) || actions.has(binding.action)) {
      return false;
    }
    actions.add(binding.action);
    if (binding.codes.length > 2 || binding.codes.length < 1) {
      return false;
    }
    for (const code of binding.codes) {
      if (!/^[A-Za-z0-9]{1,32}$/.test(code) || reserved.includes(code) || codes.has(code)) {
        return false;
      }
      codes.add(code);
    }
  }
  return moves.every((a) => actions.has(a));
}

describe("Standard (002 AK-10)", () => {
  it("DEFAULT_BINDINGS ist WASD mit leerem zweiten Platz", () => {
    expect([...DEFAULT_BINDINGS]).toEqual([
      [UP, ["KeyW"]],
      [DOWN, ["KeyS"]],
      [LEFT, ["KeyA"]],
      [RIGHT, ["KeyD"]],
    ]);
  });

  it("ACTIONS enthält jede Bewegungsaktion", () => {
    expect(ACTIONS).toEqual(MOVE_ACTIONS);
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
    ]);
  });

  it("eine fehlende Aktion bekommt ihren freien Standard", () => {
    const result = fromProto(proto([UP, ["ArrowUp"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]]));
    expect(result.get(RIGHT)).toEqual(["KeyD"]);
  });

  it("eine fehlende Aktion mit belegtem Standard ergibt die Standardbelegung", () => {
    const result = fromProto(proto([UP, ["KeyD"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]]));
    expect([...result]).toEqual([...DEFAULT_BINDINGS]);
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
    ]);
    expect(serverAccepts(toProto(result))).toBe(true);
  });

  it("eine Bewegungsaktion mit nur ungültigen Codes bekommt ihren Standard", () => {
    const result = fromProto(proto([UP, ["Enter"]], [DOWN, ["KeyS"]], [LEFT, ["KeyA"]], [RIGHT, ["KeyD"]]));
    expect(result.get(UP)).toEqual(["KeyW"]);
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

  it("eine Taste einer anderen Aktion ist ein Konflikt (AK-7)", () => {
    expect(assign(withUp("KeyW", "ArrowUp"), DOWN, 0, "KeyW")).toEqual({ kind: "conflict", other: UP });
  });

  it("die letzte Taste einer Bewegungsaktion ist blockiert (AK-7, AK-11)", () => {
    expect(assign(DEFAULT_BINDINGS, DOWN, 1, "KeyW")).toEqual({ kind: "blocked", other: UP });
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

describe("assignOverwriting (002 AK-7)", () => {
  it("löst einen Konflikt auf, der zweite Platz rückt nach", () => {
    const result = assignOverwriting(withUp("KeyW", "ArrowUp"), DOWN, 0, "KeyW");
    expect(result.kind).toBe("ok");
    if (result.kind !== "ok") {
      return;
    }
    expect(result.bindings.get(UP)).toEqual(["ArrowUp"]);
    expect(result.bindings.get(DOWN)).toEqual(["KeyW"]);
    expect(serverAccepts(toProto(result.bindings))).toBe(true);
  });

  it("löst einen Konflikt auf leerem Platz 1 auf", () => {
    const result = assignOverwriting(withUp("KeyW", "ArrowUp"), DOWN, 1, "ArrowUp");
    expect(result.kind === "ok" && result.bindings.get(UP)).toEqual(["KeyW"]);
    expect(result.kind === "ok" && result.bindings.get(DOWN)).toEqual(["KeyS", "ArrowUp"]);
  });

  it("blockiert bleibt blockiert (AK-11)", () => {
    expect(assignOverwriting(DEFAULT_BINDINGS, DOWN, 0, "KeyW")).toEqual({ kind: "blocked", other: UP });
  });

  it("reserviert bleibt reserviert (AK-8)", () => {
    expect(assignOverwriting(DEFAULT_BINDINGS, UP, 0, "Enter")).toEqual({ kind: "reserved", reservedFor: "chat" });
  });

  it("eine freie Taste wirkt wie assign (AK-5)", () => {
    const result = assignOverwriting(DEFAULT_BINDINGS, UP, 0, "ArrowUp");
    expect(result.kind === "ok" && result.bindings.get(UP)).toEqual(["ArrowUp"]);
  });
});

describe("clear und canClear (002 AK-11)", () => {
  it("die letzte Taste einer Bewegungsaktion bleibt", () => {
    expect(canClear(DEFAULT_BINDINGS, UP, 0)).toBe(false);
    expect(clear(DEFAULT_BINDINGS, UP, 0)).toBe(DEFAULT_BINDINGS);
  });

  it("ein leerer Platz ist nicht löschbar", () => {
    expect(canClear(DEFAULT_BINDINGS, UP, 1)).toBe(false);
  });

  it("bei zwei Tasten ist jeder Platz löschbar", () => {
    const b = withUp("KeyW", "ArrowUp");
    expect(canClear(b, UP, 0)).toBe(true);
    expect(canClear(b, UP, 1)).toBe(true);
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
