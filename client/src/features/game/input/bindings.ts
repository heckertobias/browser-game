import { create } from "@bufbuild/protobuf";
import { Action, KeyBindingsSchema, type KeyBindings } from "@/generated/browsergame/v1/game_pb";

/** Aktion → 0–2 Tasten (`KeyboardEvent.code`), von vorne gefüllt. Enthält immer jede Aktion aus `ACTIONS`. */
export type Bindings = ReadonlyMap<Action, readonly string[]>;

/** Platz in der Anzeige. Ein Platz hinter der letzten Taste hängt an. */
export type Slot = 0 | 1;

export type AssignResult =
  | { kind: "ok"; bindings: Bindings }
  | { kind: "conflict"; other: Action }
  | { kind: "blocked"; other: Action }
  | { kind: "reserved"; reservedFor: "menu" | "chat" };

/** Teil von `KeyboardLayoutMap` (`navigator.keyboard.getLayoutMap()`, nur Chromium; fehlt in lib.dom). */
export type KeyLayout = Pick<ReadonlyMap<string, string>, "get">;

export const MAX_CODES_PER_ACTION = 2;

/** Bewegungsaktionen brauchen mindestens eine Taste. */
export const MOVE_ACTIONS: readonly Action[] = [Action.MOVE_UP, Action.MOVE_DOWN, Action.MOVE_LEFT, Action.MOVE_RIGHT];

/** Esc öffnet das Menü, Enter (beide Tasten) den Chat. Gleich `KeyBindingRules.ReservedCodes` (Server). */
export const RESERVED_CODES: readonly string[] = ["Escape", "Enter", "NumpadEnter"];

/** WASD, zweiter Platz leer. Neue Aktionen späterer Specs kommen hier mit ihrem Standard dazu (auch leer). */
export const DEFAULT_BINDINGS: Bindings = new Map<Action, readonly string[]>([
  [Action.MOVE_UP, ["KeyW"]],
  [Action.MOVE_DOWN, ["KeyS"]],
  [Action.MOVE_LEFT, ["KeyA"]],
  [Action.MOVE_RIGHT, ["KeyD"]],
]);

/** Alle belegbaren Aktionen in Anzeigereihenfolge. */
export const ACTIONS: readonly Action[] = [...DEFAULT_BINDINGS.keys()];

/** Wie der Server: 1–32 Zeichen aus [A-Za-z0-9]. */
const CODE_PATTERN = /^[A-Za-z0-9]{1,32}$/;

function codesOf(bindings: Bindings, action: Action): readonly string[] {
  return bindings.get(action) ?? [];
}

function withCodes(bindings: Bindings, action: Action, codes: readonly string[]): Bindings {
  return new Map(bindings).set(action, codes);
}

function reservedFor(code: string): "menu" | "chat" | null {
  if (code === "Escape") {
    return "menu";
  }
  return code === "Enter" || code === "NumpadEnter" ? "chat" : null;
}

function actionWith(bindings: Bindings, code: string): Action | undefined {
  for (const [action, codes] of bindings) {
    if (codes.includes(code)) {
      return action;
    }
  }
  return undefined;
}

function place(bindings: Bindings, action: Action, slot: Slot, code: string): Bindings {
  const codes = [...codesOf(bindings, action)];
  if (slot < codes.length) {
    codes[slot] = code;
  } else {
    codes.push(code);
  }
  return withCodes(bindings, action, codes);
}

function isMove(action: Action): boolean {
  return MOVE_ACTIONS.includes(action);
}

/** Liefert immer eine Belegung, die der Server akzeptiert. */
export function fromProto(proto: KeyBindings | undefined): Bindings {
  if (!proto || proto.bindings.length === 0) {
    return DEFAULT_BINDINGS;
  }

  const used = new Set<string>();
  const present = new Map<Action, string[]>();
  for (const binding of proto.bindings) {
    if (!ACTIONS.includes(binding.action) || present.has(binding.action)) {
      continue;
    }
    const codes: string[] = [];
    for (const code of binding.codes) {
      if (codes.length >= MAX_CODES_PER_ACTION) {
        break;
      }
      if (CODE_PATTERN.test(code) && !RESERVED_CODES.includes(code) && !used.has(code)) {
        codes.push(code);
        used.add(code);
      }
    }
    present.set(binding.action, codes);
  }

  const result = new Map<Action, readonly string[]>();
  for (const action of ACTIONS) {
    const stored = present.get(action);
    if (stored && !(isMove(action) && stored.length === 0)) {
      result.set(action, stored);
      continue;
    }
    const defaults = codesOf(DEFAULT_BINDINGS, action).filter((code) => !used.has(code));
    defaults.forEach((code) => used.add(code));
    result.set(action, defaults);
  }

  if (MOVE_ACTIONS.some((action) => codesOf(result, action).length === 0)) {
    return DEFAULT_BINDINGS;
  }
  return result;
}

export function isDefault(bindings: Bindings): boolean {
  return ACTIONS.every((action) => {
    const codes = codesOf(bindings, action);
    const defaults = codesOf(DEFAULT_BINDINGS, action);
    return codes.length === defaults.length && codes.every((code, i) => code === defaults[i]);
  });
}

/** Die Standardbelegung wird als leere Nachricht gesendet (Server speichert dann nichts). */
export function toProto(bindings: Bindings): KeyBindings {
  if (isDefault(bindings)) {
    return create(KeyBindingsSchema);
  }
  return create(KeyBindingsSchema, {
    bindings: ACTIONS.map((action) => ({ action, codes: [...codesOf(bindings, action)] })),
  });
}

export function assign(bindings: Bindings, action: Action, slot: Slot, code: string): AssignResult {
  const reserved = reservedFor(code);
  if (reserved) {
    return { kind: "reserved", reservedFor: reserved };
  }
  if (!ACTIONS.includes(action) || !CODE_PATTERN.test(code) || codesOf(bindings, action).includes(code)) {
    return { kind: "ok", bindings };
  }
  const other = actionWith(bindings, code);
  if (other !== undefined) {
    return isMove(other) && codesOf(bindings, other).length === 1
      ? { kind: "blocked", other }
      : { kind: "conflict", other };
  }
  return { kind: "ok", bindings: place(bindings, action, slot, code) };
}

/** Wie `assign`, löst einen Konflikt aber auf, indem die Taste der anderen Aktion entzogen wird. */
export function assignOverwriting(
  bindings: Bindings,
  action: Action,
  slot: Slot,
  code: string,
): Exclude<AssignResult, { kind: "conflict" }> {
  const result = assign(bindings, action, slot, code);
  if (result.kind !== "conflict") {
    return result;
  }
  const freed = withCodes(
    bindings,
    result.other,
    codesOf(bindings, result.other).filter((c) => c !== code),
  );
  return { kind: "ok", bindings: place(freed, action, slot, code) };
}

export function canClear(bindings: Bindings, action: Action, slot: Slot): boolean {
  const codes = codesOf(bindings, action);
  return slot < codes.length && !(isMove(action) && codes.length === 1);
}

export function clear(bindings: Bindings, action: Action, slot: Slot): Bindings {
  if (!canClear(bindings, action, slot)) {
    return bindings;
  }
  return withCodes(
    bindings,
    action,
    codesOf(bindings, action).filter((_, i) => i !== slot),
  );
}

const ARROWS: Readonly<Record<string, string>> = { ArrowUp: "↑", ArrowDown: "↓", ArrowLeft: "←", ArrowRight: "→" };

/** Anzeigename einer Taste. Die Layout-Map (nur Chromium) hat Vorrang, sonst wird er aus dem Code abgeleitet. */
export function keyLabel(code: string, layout?: KeyLayout): string {
  const mapped = layout?.get(code);
  if (mapped) {
    const upper = mapped.toUpperCase();
    return upper.length === mapped.length ? upper : mapped;
  }
  return ARROWS[code] ?? /^(?:Key|Digit)([A-Z0-9])$/.exec(code)?.[1] ?? code;
}
