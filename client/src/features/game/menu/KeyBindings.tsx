import { useEffect, useEffectEvent, useRef, useState } from "react";
import { Action } from "@/generated/browsergame/v1/game_pb";
import { ACTIONS, canClear, keyLabel, type Bindings, type KeyLayout, type Slot } from "@/features/game/input/bindings";
import { t } from "@/features/localization/i18n";
import { ENTRY } from "./Menu";
import {
  IDLE,
  SLOTS,
  captureKey,
  fill,
  keyBindingsStep,
  loadKeyLayout,
  type KeyBindingsEvent,
  type KeyBindingsState,
  type LayoutSource,
  type Notice,
} from "./keyBindingsLogic";

const SLOT = "btn min-h-11 min-w-0 flex-1 rounded-none px-1 font-mono";
const CLEAR = "btn min-h-11 min-w-11 rounded-none px-0 font-mono";
const CONFIRM = "btn btn-lg min-h-11 flex-1 rounded-none font-mono uppercase";

const ACTION_NAMES: ReadonlyMap<Action, string> = new Map([
  [Action.MOVE_UP, t.actionMoveUp],
  [Action.MOVE_DOWN, t.actionMoveDown],
  [Action.MOVE_LEFT, t.actionMoveLeft],
  [Action.MOVE_RIGHT, t.actionMoveRight],
]);

export function actionName(action: Action): string {
  return ACTION_NAMES.get(action) ?? Action[action];
}

export function slotId(action: Action, slot: Slot): string {
  return `binding-slot-${String(action)}-${String(slot)}`;
}

function noticeText(notice: Notice): string {
  if (notice.kind === "blocked") return fill(t.bindingBlocked, { action: actionName(notice.other) });
  return notice.reservedFor === "menu" ? t.bindingReserved : t.bindingReservedChat;
}

export interface KeyBindingsViewProps {
  bindings: Bindings | null;
  state: KeyBindingsState;
  layout: KeyLayout | undefined;
  onEvent: (event: KeyBindingsEvent) => void;
}

export function KeyBindingsView({ bindings, state, layout, onEvent }: KeyBindingsViewProps) {
  return (
    <div data-testid="key-bindings" className="flex w-full flex-col gap-4">
      {bindings === null ? (
        <p role="status" data-testid="binding-loading" className="normal-case">{t.statusConnecting}</p>
      ) : (
        <>
          <table className="w-full table-fixed border-separate border-spacing-1">
            <thead>
              <tr>
                <th scope="col" className="text-left">{t.bindingAction}</th>
                {SLOTS.map((slot) => (
                  <th key={slot} scope="col">{fill(t.bindingSlot, { n: String(slot + 1) })}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {ACTIONS.map((action) => (
                <tr key={action} data-testid={`binding-row-${String(action)}`}>
                  <th scope="row" className="text-left">{actionName(action)}</th>
                  {SLOTS.map((slot) => {
                    const code = bindings.get(action)?.[slot];
                    const recording = state.mode === "recording" && state.action === action && state.slot === slot;
                    return (
                      <td key={slot}>
                        <div className="flex gap-1">
                          <button
                            id={slotId(action, slot)}
                            type="button"
                            data-testid={`binding-${String(action)}-${String(slot)}`}
                            aria-pressed={recording}
                            className={recording ? `${SLOT} btn-primary` : SLOT}
                            onClick={() => { onEvent({ type: "record", action, slot }); }}
                          >
                            {recording ? t.bindingPressKey : code === undefined ? "—" : keyLabel(code, layout)}
                          </button>
                          <button
                            type="button"
                            data-testid={`binding-clear-${String(action)}-${String(slot)}`}
                            aria-label={t.bindingClear}
                            title={t.bindingClear}
                            disabled={!canClear(bindings, action, slot)}
                            className={CLEAR}
                            onClick={() => { onEvent({ type: "clear", action, slot }); }}
                          >
                            ×
                          </button>
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
          {state.mode === "confirm" && (
            <div data-testid="binding-confirm" className="flex flex-col gap-2">
              <p role="status" className="normal-case">
                {fill(t.bindingConflict, { key: keyLabel(state.code, layout), action: actionName(state.other) })}
              </p>
              <div className="flex gap-2">
                <button type="button" data-testid="binding-overwrite" className={`${CONFIRM} btn-primary`} onClick={() => { onEvent({ type: "overwrite" }); }}>
                  {t.bindingOverwrite}
                </button>
                <button type="button" data-testid="binding-cancel" autoFocus className={CONFIRM} onClick={() => { onEvent({ type: "cancel" }); }}>
                  {t.bindingCancel}
                </button>
              </div>
            </div>
          )}
          {state.mode === "idle" && state.notice && (
            <p role="status" data-testid="binding-notice" className="normal-case">{noticeText(state.notice)}</p>
          )}
          <button type="button" data-testid="binding-reset" className={ENTRY} onClick={() => { onEvent({ type: "reset" }); }}>
            {t.bindingReset}
          </button>
        </>
      )}
    </div>
  );
}

export interface KeyBindingsProps {
  bindings: Bindings | null;
  onSave: (bindings: Bindings) => void;
}

export function KeyBindings({ bindings, onSave }: KeyBindingsProps) {
  const [state, setState] = useState<KeyBindingsState>(IDLE);
  const [layout, setLayout] = useState<KeyLayout | undefined>(undefined);
  const pendingKeyUp = useRef<string | null>(null);

  const apply = (event: KeyBindingsEvent) => {
    if (!bindings) return;
    // Der „×“-Button wird nach dem Leeren oft disabled; der Fokus ginge sonst verloren.
    let back: string | null = null;
    if (state.mode === "confirm" && (event.type === "cancel" || event.type === "overwrite")) {
      back = slotId(state.action, state.slot);
    } else if (event.type === "clear") {
      back = slotId(event.action, event.slot);
    }
    const next = keyBindingsStep(state, bindings, event);
    setState(next.state);
    if (next.save) onSave(next.save);
    if (back) document.getElementById(back)?.focus();
  };

  const onKeyDown = useEffectEvent((e: KeyboardEvent) => {
    const key = captureKey(e, state.mode);
    if (!key || key.type === "ignore") return;
    if (key.type === "key") pendingKeyUp.current = e.code;
    apply(key);
  });

  const listening = state.mode !== "idle";

  // Capture: vor dem Esc-Listener in App, dem Chat und Buttons.
  useEffect(() => {
    if (!listening) return;
    const abort = new AbortController();
    window.addEventListener("keydown", (e) => { onKeyDown(e); }, { capture: true, signal: abort.signal });
    return () => { abort.abort(); };
  }, [listening]);

  // Leertaste aktiviert Buttons erst beim Loslassen; sonst startete sie die Aufnahme gleich wieder.
  useEffect(() => {
    const abort = new AbortController();
    window.addEventListener(
      "keyup",
      (e) => {
        if (e.code !== pendingKeyUp.current) return;
        pendingKeyUp.current = null;
        e.preventDefault();
      },
      { capture: true, signal: abort.signal },
    );
    return () => { abort.abort(); };
  }, []);

  useEffect(() => {
    let live = true;
    void loadKeyLayout(navigator as unknown as LayoutSource).then((map) => {
      if (live) setLayout(map);
    });
    return () => { live = false; };
  }, []);

  return <KeyBindingsView bindings={bindings} state={state} layout={layout} onEvent={apply} />;
}
