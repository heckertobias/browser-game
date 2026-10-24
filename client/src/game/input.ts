import { POINTER_DEAD_ZONE } from "./constants";
import { clampDirection, ZERO, type Vec } from "./vector";

export type Action = "moveUp" | "moveDown" | "moveLeft" | "moveRight";

/** Aktion → Tasten (`KeyboardEvent.code`, unabhängig vom Tastaturlayout). Umbelegen kommt mit 002. */
export type KeyBindings = Readonly<Record<Action, readonly string[]>>;

export const DEFAULT_BINDINGS: KeyBindings = {
  moveUp: ["KeyW"],
  moveDown: ["KeyS"],
  moveLeft: ["KeyA"],
  moveRight: ["KeyD"],
};

export function activeActions(pressedKeys: ReadonlySet<string>, bindings: KeyBindings): Set<Action> {
  const actions = new Set<Action>();
  for (const [action, keys] of Object.entries(bindings) as [Action, readonly string[]][]) {
    if (keys.some((key) => pressedKeys.has(key))) {
      actions.add(action);
    }
  }
  return actions;
}

export function keyboardDirection(actions: ReadonlySet<Action>): Vec {
  const x = (actions.has("moveRight") ? 1 : 0) - (actions.has("moveLeft") ? 1 : 0);
  const y = (actions.has("moveDown") ? 1 : 0) - (actions.has("moveUp") ? 1 : 0);
  return clampDirection(x, y);
}

/** Halten-und-Folgen: volle Geschwindigkeit Richtung Zeiger, in der Totzone Stillstand. */
export function pointerDirection(pointer: Vec, player: Vec): Vec {
  const dx = pointer.x - player.x;
  const dy = pointer.y - player.y;
  const distance = Math.hypot(dx, dy);
  return distance <= POINTER_DEAD_ZONE ? ZERO : { x: dx / distance, y: dy / distance };
}

/** Sobald eine Bewegungstaste gedrückt ist, hat die Tastatur Vorrang vor dem Zeiger. */
export function combinedDirection(actions: ReadonlySet<Action>, pointer: Vec | null, player: Vec): Vec {
  if (actions.size > 0) {
    return keyboardDirection(actions);
  }
  return pointer ? pointerDirection(pointer, player) : ZERO;
}

/** Hält gedrückte Tasten und den gedrückten Zeiger (Maus und Touch einheitlich über Pointer Events). */
export class InputTracker {
  private readonly pressedKeys = new Set<string>();
  private pointerClient: { x: number; y: number } | null = null;
  private activePointerId: number | null = null;
  private readonly abort = new AbortController();

  constructor(
    private readonly canvas: HTMLCanvasElement,
    private readonly bindings: KeyBindings = DEFAULT_BINDINGS,
  ) {
    const signal = this.abort.signal;
    window.addEventListener("keydown", (e) => { this.pressedKeys.add(e.code); }, { signal });
    window.addEventListener("keyup", (e) => { this.pressedKeys.delete(e.code); }, { signal });
    window.addEventListener("blur", () => { this.reset(); }, { signal });
    document.addEventListener("visibilitychange", () => { if (document.hidden) this.reset(); }, { signal });

    canvas.addEventListener("pointerdown", (e) => {
      if (this.activePointerId !== null) return;
      this.activePointerId = e.pointerId;
      canvas.setPointerCapture(e.pointerId);
      this.pointerClient = { x: e.clientX, y: e.clientY };
      e.preventDefault();
    }, { signal });
    canvas.addEventListener("pointermove", (e) => {
      if (e.pointerId === this.activePointerId) this.pointerClient = { x: e.clientX, y: e.clientY };
    }, { signal });
    const release = (e: PointerEvent) => {
      if (e.pointerId !== this.activePointerId) return;
      this.activePointerId = null;
      this.pointerClient = null;
    };
    canvas.addEventListener("pointerup", release, { signal });
    canvas.addEventListener("pointercancel", release, { signal });
    canvas.addEventListener("contextmenu", (e) => { e.preventDefault(); }, { signal });
  }

  /**
   * Aktuelle Wunschrichtung. `toWorld` rechnet Bildschirm- in Weltkoordinaten um,
   * `player` ist die eigene (vorhergesagte) Position.
   */
  direction(player: Vec, toWorld: (clientX: number, clientY: number, canvasRect: DOMRect) => Vec): Vec {
    const pointer = this.pointerClient
      ? toWorld(this.pointerClient.x, this.pointerClient.y, this.canvas.getBoundingClientRect())
      : null;
    return combinedDirection(activeActions(this.pressedKeys, this.bindings), pointer, player);
  }

  dispose(): void {
    this.abort.abort();
  }

  private reset(): void {
    this.pressedKeys.clear();
    this.activePointerId = null;
    this.pointerClient = null;
  }
}
