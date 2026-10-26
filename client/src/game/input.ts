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

/** Felder eines Pointer-Events, die PointerHold braucht (ohne DOM testbar). */
export type PointerSample = Pick<PointerEvent, "pointerId" | "isPrimary" | "button" | "clientX" | "clientY">;

/** Diese Events beenden das Halten wie pointerup (Capture kann ohne pointerup verloren gehen). */
export const POINTER_RELEASE_EVENTS = ["pointerup", "pointercancel", "lostpointercapture"] as const;

/** Tippen und Halten (AK-7): nur der primäre Zeiger mit Haupttaste; weitere Finger werden ignoriert. */
export class PointerHold {
  private pointerId: number | null = null;
  private client: { x: number; y: number } | null = null;

  /** true, wenn dieser Zeiger das Halten startet – dann Capture setzen und preventDefault. */
  down(e: PointerSample): boolean {
    if (this.pointerId !== null || !e.isPrimary || e.button !== 0) return false;
    this.pointerId = e.pointerId;
    this.client = { x: e.clientX, y: e.clientY };
    return true;
  }

  move(e: Pick<PointerSample, "pointerId" | "clientX" | "clientY">): void {
    if (e.pointerId === this.pointerId) this.client = { x: e.clientX, y: e.clientY };
  }

  release(pointerId: number): void {
    if (pointerId === this.pointerId) this.reset();
  }

  reset(): void {
    this.pointerId = null;
    this.client = null;
  }

  /** Gehaltene Position in Client-Koordinaten oder null. */
  position(): { x: number; y: number } | null {
    return this.client;
  }
}

/** Hält gedrückte Tasten und den gedrückten Zeiger (Maus und Touch einheitlich über Pointer Events). */
export class InputTracker {
  private readonly pressedKeys = new Set<string>();
  private readonly hold = new PointerHold();
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
      if (!this.hold.down(e)) return;
      canvas.setPointerCapture(e.pointerId);
      e.preventDefault();
    }, { signal });
    canvas.addEventListener("pointermove", (e) => { this.hold.move(e); }, { signal });
    const release = (e: PointerEvent) => { this.hold.release(e.pointerId); };
    for (const type of POINTER_RELEASE_EVENTS) canvas.addEventListener(type, release, { signal });
    canvas.addEventListener("contextmenu", (e) => { e.preventDefault(); }, { signal });

    // iOS ignoriert user-scalable=no beim Pinch (AK-8); GestureEvent fehlt in lib.dom.
    const preventGesture = (e: Event) => { e.preventDefault(); };
    document.addEventListener("gesturestart", preventGesture, { signal, passive: false });
    document.addEventListener("gesturechange", preventGesture, { signal, passive: false });
  }

  /**
   * Aktuelle Wunschrichtung. `toWorld` rechnet Bildschirm- in Weltkoordinaten um,
   * `player` ist die eigene (vorhergesagte) Position.
   */
  direction(player: Vec, toWorld: (clientX: number, clientY: number, canvasRect: DOMRect) => Vec): Vec {
    const client = this.hold.position();
    const pointer = client ? toWorld(client.x, client.y, this.canvas.getBoundingClientRect()) : null;
    return combinedDirection(activeActions(this.pressedKeys, this.bindings), pointer, player);
  }

  dispose(): void {
    this.abort.abort();
  }

  private reset(): void {
    this.pressedKeys.clear();
    this.hold.reset();
  }
}
