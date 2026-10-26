/** Was ScreenWakeLock von `WakeLockSentinel` braucht (in Tests gefälscht). */
export interface WakeLockSentinelLike {
  readonly released: boolean;
  release(): Promise<void>;
}

/** `navigator.wakeLock`; fehlt ohne Unterstützung oder außerhalb von HTTPS (dann `undefined`). */
export interface WakeLockApi {
  request(type: "screen"): Promise<WakeLockSentinelLike>;
}

/** Ausschnitt von `document`, den ScreenWakeLock braucht (ohne DOM testbar). */
export interface WakeLockDocument {
  readonly visibilityState: DocumentVisibilityState;
  addEventListener(type: string, listener: () => void, options: { signal: AbortSignal }): void;
}

/** Eingaben, nach denen erneut angefordert wird (Browser, die eine Nutzergeste verlangen); bei Touch ist erst pointerup eine Geste. */
export const WAKE_LOCK_INPUT_EVENTS = ["pointerup", "keydown"] as const;

/** Hält den Bildschirm an, solange das Spiel sichtbar ist (AK-14); ohne Unterstützung oder bei Ablehnung still. */
export class ScreenWakeLock {
  private sentinel: WakeLockSentinelLike | null = null;
  private pending = false;
  private abort: AbortController | null = null;
  /** Nach Start bzw. Sichtbarwerden darf eine Eingabe einmal neu anfordern. */
  private retryOnInput = false;

  constructor(private readonly api: WakeLockApi | undefined, private readonly doc: WakeLockDocument) {}

  start(): void {
    if (!this.api || this.abort) return;
    this.abort = new AbortController();
    const signal = this.abort.signal;
    // Der Browser gibt die Sperre beim Verstecken selbst frei (sentinel.released = true).
    this.doc.addEventListener("visibilitychange", () => {
      if (this.doc.visibilityState !== "visible") return;
      this.retryOnInput = true;
      this.request();
    }, { signal });
    const onInput = () => {
      if (this.retryOnInput && this.request()) this.retryOnInput = false;
    };
    for (const type of WAKE_LOCK_INPUT_EVENTS) this.doc.addEventListener(type, onInput, { signal });
    this.retryOnInput = true;
    this.request();
  }

  stop(): void {
    this.abort?.abort();
    this.abort = null;
    const sentinel = this.sentinel;
    this.sentinel = null;
    if (sentinel) sentinel.release().catch(() => { /* schon freigegeben */ });
  }

  /** true, wenn eine Anforderung gestartet wurde. */
  private request(): boolean {
    const api = this.api;
    if (!api || !this.abort || this.pending || this.doc.visibilityState !== "visible") return false;
    if (this.sentinel && !this.sentinel.released) return false;
    this.pending = true;
    void this.acquire(api);
    return true;
  }

  private async acquire(api: WakeLockApi): Promise<void> {
    try {
      const sentinel = await api.request("screen");
      if (this.abort) this.sentinel = sentinel;
      else await sentinel.release(); // stop() kam, während die Anforderung lief
    } catch {
      // abgelehnt (NotAllowedError, Akku-Sparmodus): normale Einstellung des Geräts
    } finally {
      this.pending = false;
    }
  }
}
