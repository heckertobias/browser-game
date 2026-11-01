export type ServerState = "unreachable" | "same" | "update";
export const PLAY_PATH = "/play";
export const SERVER_POLL_MS = 5000;
export const SERVER_TIMEOUT_MS = 5000;

const SCRIPT_TAG = /<script\b[^>]*>/gi;
const MODULE_TYPE = /\btype\s*=\s*["']?module["'\s>]/i;
const SRC = /\bsrc\s*=\s*["']?([^"'\s>]+)/i;

/** `src` des ersten Modul-Skripts mit `src` (Vite: /play/assets/index-<hash>.js), sonst null. */
export function entryScript(html: string): string | null {
  for (const [tag] of html.matchAll(SCRIPT_TAG)) {
    if (!MODULE_TYPE.test(tag)) continue;
    const src = SRC.exec(tag)?.[1];
    if (src) return src;
  }
  return null;
}

export function classifyServer(
  current: string | null,
  response: { ok: boolean; redirected: boolean },
  html: string,
): ServerState {
  if (!response.ok) return "unreachable";
  if (response.redirected) return "same"; // z. B. Anmeldung abgelaufen: Neuladen führt zur Anmeldung
  const served = entryScript(html);
  return current !== null && served !== null && served !== current ? "update" : "same";
}

export type FetchPage = (
  url: string,
  init: RequestInit,
) => Promise<{ ok: boolean; redirected: boolean; text(): Promise<string> }>;
export interface WatchTimers {
  setTimeout(handler: () => void, timeout: number): number;
  clearTimeout(id: number): void;
}
export interface WatchTarget {
  addEventListener(type: string, listener: (event: Event) => void, options: { signal: AbortSignal }): void;
}
export interface WatchDocument extends WatchTarget {
  readonly visibilityState: DocumentVisibilityState;
}
export interface ServerWatchDeps {
  fetch: FetchPage;
  timers: WatchTimers;
  doc: WatchDocument;
  win: WatchTarget;
}

/** Fragt bei getrennter Verbindung /play ab, bis eine neue Version läuft (AK-17); nur sichtbar, nie parallel. */
export class ServerWatch {
  private abort: AbortController | null = null;
  private timer: number | null = null;
  /** Sitzung, deren Abfrage gerade läuft; eine alte Abfrage darf eine neue Sitzung nicht stören. */
  private running: AbortController | null = null;
  private request: AbortController | null = null;

  constructor(
    private readonly current: string | null,
    private readonly onUpdate: () => void,
    private readonly deps: ServerWatchDeps,
  ) {}

  start(): void {
    if (this.abort) return;
    this.abort = new AbortController();
    const signal = this.abort.signal;
    const now = () => {
      void this.check();
    };
    this.deps.doc.addEventListener("visibilitychange", now, { signal }); // verborgen: check() kehrt sofort zurück
    this.deps.win.addEventListener("online", now, { signal });
    now();
  }

  stop(): void {
    this.abort?.abort();
    this.abort = null;
    this.request?.abort();
    this.cancelTimer();
  }

  private async check(): Promise<void> {
    const session = this.abort;
    if (!session || this.running === session || this.deps.doc.visibilityState !== "visible") return;
    this.cancelTimer(); // online/sichtbar vor Ablauf: keine zweite Kette
    this.running = session;
    const request = new AbortController();
    this.request = request;
    const timeout = this.deps.timers.setTimeout(() => {
      request.abort();
    }, SERVER_TIMEOUT_MS);
    let state: ServerState = "unreachable";
    try {
      const response = await this.deps.fetch(PLAY_PATH, {
        cache: "no-store",
        credentials: "same-origin",
        signal: request.signal,
      });
      const html = response.ok && !response.redirected ? await response.text() : "";
      state = classifyServer(this.current, response, html);
    } catch {
      // Netzfehler oder Timeout: weiter warten
    } finally {
      this.deps.timers.clearTimeout(timeout);
      if (this.running === session) this.running = null;
      if (this.request === request) this.request = null;
    }
    if (this.abort !== session) return; // inzwischen gestoppt (oder neu gestartet)
    if (state === "update") {
      this.stop();
      this.onUpdate();
      return;
    }
    this.timer = this.deps.timers.setTimeout(() => {
      this.timer = null;
      void this.check();
    }, SERVER_POLL_MS);
  }

  private cancelTimer(): void {
    if (this.timer === null) return;
    this.deps.timers.clearTimeout(this.timer);
    this.timer = null;
  }
}
