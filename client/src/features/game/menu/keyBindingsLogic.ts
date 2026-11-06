import type { Action } from "@/generated/browsergame/v1/game_pb";
import {
  DEFAULT_BINDINGS,
  assign,
  assignOverwriting,
  canClear,
  clear,
  isDefault,
  type AssignResult,
  type Bindings,
  type KeyLayout,
  type Slot,
} from "@/features/game/input/bindings";
import { menuKeyAction } from "./menuLogic";

export const SLOTS: readonly Slot[] = [0, 1];

/** Hinweis nach einer abgelehnten Taste. */
export type Notice = Extract<AssignResult, { kind: "blocked" | "reserved" }>;

export type KeyBindingsState =
  | { mode: "idle"; notice: Notice | null }
  | { mode: "recording"; action: Action; slot: Slot }
  | { mode: "confirm"; action: Action; slot: Slot; code: string; other: Action };

export const IDLE: KeyBindingsState = { mode: "idle", notice: null };

export type KeyBindingsEvent =
  | { type: "record"; action: Action; slot: Slot }
  | { type: "key"; code: string }
  | { type: "cancel" }
  | { type: "overwrite" }
  | { type: "clear"; action: Action; slot: Slot }
  | { type: "reset" };

export interface KeyBindingsStep {
  state: KeyBindingsState;
  /** Neue Belegung, die sofort gilt und gespeichert wird; `null` = nichts geändert. */
  save: Bindings | null;
}

function settle(result: Exclude<AssignResult, { kind: "conflict" }>, bindings: Bindings): KeyBindingsStep {
  if (result.kind === "ok") {
    // `assign` gibt bei „keine Änderung“ dasselbe Objekt zurück.
    return { state: IDLE, save: result.bindings === bindings ? null : result.bindings };
  }
  return { state: { mode: "idle", notice: result }, save: null };
}

export function keyBindingsStep(state: KeyBindingsState, bindings: Bindings, event: KeyBindingsEvent): KeyBindingsStep {
  switch (event.type) {
    case "record":
      if (state.mode === "recording" && state.action === event.action && state.slot === event.slot) {
        return { state: IDLE, save: null };
      }
      return { state: { mode: "recording", action: event.action, slot: event.slot }, save: null };
    case "key": {
      if (state.mode !== "recording") return { state, save: null };
      const result = assign(bindings, state.action, state.slot, event.code);
      if (result.kind === "conflict") {
        return {
          state: { mode: "confirm", action: state.action, slot: state.slot, code: event.code, other: result.other },
          save: null,
        };
      }
      return settle(result, bindings);
    }
    case "cancel":
      return { state: IDLE, save: null };
    case "overwrite":
      if (state.mode !== "confirm") return { state, save: null };
      return settle(assignOverwriting(bindings, state.action, state.slot, state.code), bindings);
    case "clear":
      return {
        state: IDLE,
        save: canClear(bindings, event.action, event.slot) ? clear(bindings, event.action, event.slot) : null,
      };
    case "reset":
      return { state: IDLE, save: isDefault(bindings) ? null : DEFAULT_BINDINGS };
  }
}

export type CapturedKey = { type: "key"; code: string } | { type: "cancel" } | { type: "ignore" };

export type CaptureEvent = Pick<KeyboardEvent, "key" | "code" | "repeat" | "target" | "preventDefault" | "stopImmediatePropagation">;

/**
 * Tastendruck im Capture-Listener der Tastenbelegung. Beim Aufnehmen gehört jede Taste der Aufnahme:
 * Sie löst keine Buttons aus, öffnet weder Chat (AK-8) noch schließt sie das Menü. In der Rückfrage nur Esc.
 * `null`: Event unberührt lassen.
 */
export function captureKey(e: CaptureEvent, mode: KeyBindingsState["mode"]): CapturedKey | null {
  if (mode === "idle") return null;
  const escape = menuKeyAction(e, true) === "cancelRecording";
  if (mode === "confirm" && !escape) return null;
  e.preventDefault();
  e.stopImmediatePropagation();
  if (mode === "confirm") return { type: "cancel" };
  if (e.repeat) return { type: "ignore" };
  if (escape) return { type: "cancel" };
  if (e.code === "" || e.code === "Unidentified") return { type: "ignore" };
  return { type: "key", code: e.code };
}

/** Teil von `navigator.keyboard` (Keyboard Map API, nur Chromium; fehlt in lib.dom). */
export interface LayoutSource {
  keyboard?: { getLayoutMap?: () => Promise<KeyLayout> };
}

/** Layout-Map für die Tastennamen oder `undefined` (andere Browser, Fehler wie in Frames ohne Berechtigung). */
export async function loadKeyLayout(source: LayoutSource): Promise<KeyLayout | undefined> {
  try {
    return await source.keyboard?.getLayoutMap?.();
  } catch {
    return undefined;
  }
}

/** Ersetzt `{name}` in einem i18n-Text; unbekannte Platzhalter bleiben stehen. */
export function fill(template: string, values: Readonly<Record<string, string>>): string {
  return template.replace(/\{(\w+)\}/g, (match: string, name: string) => values[name] ?? match);
}
