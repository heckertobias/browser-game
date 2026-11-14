import { POINTER_DEAD_ZONE } from "@/features/game/core/constants";
import { clampDirection, ZERO, type Vec } from "@/features/game/core/vector";
import { Action, Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { DEFAULT_BINDINGS, type Bindings } from "./bindings";

/** Aktionen, deren Tasten gerade gedrückt sind; jeder Platz einer Aktion löst sie aus. */
export function activeActions(pressedKeys: ReadonlySet<string>, bindings: Bindings): Set<Action> {
  const actions = new Set<Action>();
  for (const [action, codes] of bindings) {
    if (codes.some((code) => pressedKeys.has(code))) {
      actions.add(action);
    }
  }
  return actions;
}

export function keyboardDirection(actions: ReadonlySet<Action>): Vec {
  const x = (actions.has(Action.MOVE_RIGHT) ? 1 : 0) - (actions.has(Action.MOVE_LEFT) ? 1 : 0);
  const y = (actions.has(Action.MOVE_DOWN) ? 1 : 0) - (actions.has(Action.MOVE_UP) ? 1 : 0);
  return clampDirection(x, y);
}

/** Halten-und-Folgen: volle Geschwindigkeit Richtung Zeiger, in der Totzone Stillstand. */
export function pointerDirection(pointer: Vec, player: Vec): Vec {
  const dx = pointer.x - player.x;
  const dy = pointer.y - player.y;
  const distance = Math.hypot(dx, dy);
  return distance <= POINTER_DEAD_ZONE ? ZERO : { x: dx / distance, y: dy / distance };
}

const MOVE_ACTIONS: ReadonlySet<Action> = new Set([Action.MOVE_UP, Action.MOVE_DOWN, Action.MOVE_LEFT, Action.MOVE_RIGHT]);

const ATTACK_KINDS: ReadonlyMap<Action, Attack_Kind> = new Map([
  [Action.ATTACK_MELEE, Attack_Kind.MELEE],
  [Action.ATTACK_RANGED, Attack_Kind.RANGED],
]);

/** Angriffsart der Taste nach der Belegung oder null (nicht belegt oder keine Angriffsaktion, 013). */
export function attackKindFor(code: string, bindings: Bindings): Attack_Kind | null {
  for (const [action, codes] of bindings) {
    const kind = ATTACK_KINDS.get(action);
    if (kind !== undefined && codes.includes(code)) return kind;
  }
  return null;
}

/** Sobald eine Bewegungstaste gedrückt ist, hat die Tastatur Vorrang vor dem Zeiger. */
export function combinedDirection(actions: ReadonlySet<Action>, pointer: Vec | null, player: Vec): Vec {
  if ([...actions].some((action) => MOVE_ACTIONS.has(action))) {
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

  /** Startet dieser Zeiger etwas? Nur der primäre mit Haupttaste, solange kein anderer hält. */
  accepts(e: Pick<PointerSample, "isPrimary" | "button">): boolean {
    return this.pointerId === null && e.isPrimary && e.button === 0;
  }

  /** true, wenn dieser Zeiger das Halten startet – dann Capture setzen und preventDefault. */
  down(e: PointerSample): boolean {
    if (!this.accepts(e)) return false;
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

const EDITABLE_TAGS: ReadonlySet<string> = new Set(["INPUT", "TEXTAREA", "SELECT"]);

/** Hier tippt der Spieler: Tasten steuern dann nicht die Figur (011 AK-6). Ohne instanceof, damit ohne DOM testbar. */
export function isEditableTarget(target: EventTarget | null): boolean {
  const element = target as Partial<HTMLElement> | null;
  return element?.isContentEditable === true || EDITABLE_TAGS.has(element?.tagName ?? "");
}

/** Rückfragen des InputTrackers an das Spiel (013). Beide optional. */
export interface InputCallbacks {
  /** Liegt unter dem Druck ein Ziel, wählt der Aufrufer es aus und gibt true zurück; dann startet kein Halten. */
  pickTarget?(clientX: number, clientY: number): boolean;
  /** Eine belegte Angriffstaste wurde gedrückt (einmal je Druck, ohne Wiederholung). */
  onAttack?(kind: Attack_Kind): void;
}

/** Hält gedrückte Tasten und den gedrückten Zeiger (Maus und Touch einheitlich über Pointer Events). */
export class InputTracker {
  private readonly pressedKeys = new Set<string>();
  private readonly hold = new PointerHold();
  private readonly abort = new AbortController();
  private bindings: Bindings = DEFAULT_BINDINGS;
  private suspended = false;

  constructor(
    private readonly canvas: HTMLCanvasElement,
    private readonly callbacks: InputCallbacks = {},
  ) {
    const signal = this.abort.signal;
    window.addEventListener("keydown", (e) => {
      if (this.suspended || isEditableTarget(e.target)) return;
      this.pressedKeys.add(e.code);
      if (e.repeat) return;
      const kind = attackKindFor(e.code, this.bindings);
      if (kind !== null) this.callbacks.onAttack?.(kind);
    }, { signal });
    window.addEventListener("keyup", (e) => { this.pressedKeys.delete(e.code); }, { signal });
    // Nur die Tasten, nicht reset(): ein gehaltener Zeiger läuft weiter.
    window.addEventListener("focusin", (e) => { if (isEditableTarget(e.target)) this.pressedKeys.clear(); }, { signal });
    window.addEventListener("blur", () => { this.reset(); }, { signal });
    document.addEventListener("visibilitychange", () => { if (document.hidden) this.reset(); }, { signal });

    canvas.addEventListener("pointerdown", (e) => {
      if (this.suspended || !this.hold.accepts(e)) return;
      // preventDefault unten verhindert auch den Fokuswechsel; das Feld behielte den Fokus und die Tasten blieben tot.
      const active = document.activeElement;
      if (isEditableTarget(active)) (active as HTMLElement).blur();
      e.preventDefault();
      // Ein Ziel unter dem Zeiger wird ausgewählt statt hinzulaufen (013, plan.md Entscheidung 7).
      if (this.callbacks.pickTarget?.(e.clientX, e.clientY) === true) return;
      this.hold.down(e);
      canvas.setPointerCapture(e.pointerId);
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

  /** Neue Belegung gilt ab dem nächsten `direction()` (AK-5). */
  setBindings(bindings: Bindings): void {
    this.bindings = bindings;
  }

  /**
   * Menü offen (AK-4): gedrückte Tasten und gehaltener Zeiger verfallen, neue werden ignoriert, die Figur steht.
   * Eine über das Schließen hinaus gehaltene Taste zählt erst wieder mit ihrer nächsten Wiederholung.
   */
  setSuspended(suspended: boolean): void {
    this.suspended = suspended;
    if (suspended) this.reset();
  }

  /**
   * Aktuelle Wunschrichtung. `toWorld` rechnet Bildschirm- in Weltkoordinaten um,
   * `player` ist die eigene (vorhergesagte) Position.
   */
  direction(player: Vec, toWorld: (clientX: number, clientY: number, canvasRect: DOMRect) => Vec): Vec {
    if (this.suspended) return ZERO;
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
