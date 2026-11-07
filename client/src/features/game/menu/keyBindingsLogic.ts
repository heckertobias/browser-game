import type { Action } from "@/generated/browsergame/v1/game_pb";
import {
  DEFAULT_BINDINGS,
  assign,
  clear,
  isDefault,
  type AssignResult,
  type Bindings,
  type KeyLayout,
  type Slot,
} from "@/features/game/input/bindings";
import { menuKeyAction } from "./menuLogic";

export const SLOTS: readonly Slot[] = [0, 1];

/** Hinweis nach einer Taste: abgelehnt (AK-8) oder einer anderen Aktion weggenommen (AK-7). */
export type Notice =
  | Extract<AssignResult, { kind: "reserved" }>
  | { kind: "overwritten"; code: string; from: Action; to: Action };

export type KeyBindingsState =
  | { mode: "idle"; notice: Notice | null }
  | { mode: "recording"; action: Action; slot: Slot };

export const IDLE: KeyBindingsState = { mode: "idle", notice: null };

export type KeyBindingsEvent =
  | { type: "record"; action: Action; slot: Slot }
  | { type: "key"; code: string }
  | { type: "cancel" }
  | { type: "clear"; action: Action; slot: Slot }
  | { type: "reset" };

export interface KeyBindingsStep {
  state: KeyBindingsState;
  /** Neue Belegung, die sofort gilt und gespeichert wird; `null` = nichts geändert. */
  save: Bindings | null;
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
      if (result.kind === "reserved") return { state: { mode: "idle", notice: result }, save: null };
      const notice: Notice | null =
        result.overwritten === undefined
          ? null
          : { kind: "overwritten", code: event.code, from: result.overwritten, to: state.action };
      // `assign` gibt bei „keine Änderung“ dasselbe Objekt zurück.
      return { state: notice ? { mode: "idle", notice } : IDLE, save: result.bindings === bindings ? null : result.bindings };
    }
    case "cancel":
      return { state: IDLE, save: null };
    case "clear": {
      const next = clear(bindings, event.action, event.slot);
      return { state: IDLE, save: next === bindings ? null : next };
    }
    case "reset":
      return { state: IDLE, save: isDefault(bindings) ? null : DEFAULT_BINDINGS };
  }
}

export type CapturedKey = { type: "key"; code: string } | { type: "cancel" } | { type: "ignore" };

export type CaptureEvent = Pick<KeyboardEvent, "key" | "code" | "repeat" | "target" | "preventDefault" | "stopImmediatePropagation">;

/**
 * Tastendruck im Capture-Listener der Tastenbelegung. Beim Aufnehmen gehört jede Taste der Aufnahme:
 * Sie löst keine Buttons aus, öffnet weder Chat (AK-8) noch schließt sie das Menü.
 * `null`: Event unberührt lassen.
 */
export function captureKey(e: CaptureEvent, mode: KeyBindingsState["mode"]): CapturedKey | null {
  if (mode === "idle") return null;
  const escape = menuKeyAction(e, true) === "cancelRecording";
  e.preventDefault();
  e.stopImmediatePropagation();
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
