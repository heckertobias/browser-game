import { describe, expect, it } from "vitest";
import {
  PLAY_PATH,
  SERVER_POLL_MS,
  SERVER_TIMEOUT_MS,
  ServerWatch,
  classifyServer,
  entryScript,
  type FetchPage,
} from "./update";

const VITE = `<!doctype html><html><head>
<script type="module" crossorigin src="/play/assets/index-BFl2WypM.js"></script>
<link rel="stylesheet" href="/play/assets/index-x.css"></head><body></body></html>`;

describe("entryScript", () => {
  it("findet die Vite-Zeile", () => {
    expect(entryScript(VITE)).toBe("/play/assets/index-BFl2WypM.js");
  });

  it("findet src vor type", () => {
    expect(entryScript('<script src="/a.js" type="module"></script>')).toBe("/a.js");
  });

  it("findet einfache Anführungszeichen", () => {
    expect(entryScript("<script type='module' src='/b.js'></script>")).toBe("/b.js");
  });

  it("überspringt Modul-Skripte ohne src", () => {
    expect(entryScript('<script type="module">import("x")</script><script type="module" src="/c.js"></script>')).toBe("/c.js");
  });

  it("liefert null bei nur klassischem Skript", () => {
    expect(entryScript('<html><script src="/login.js"></script></html>')).toBeNull();
  });
});

describe("classifyServer", () => {
  const ok = { ok: true, redirected: false };
  it.each([502, 503, 504, 404])("%i ist unreachable", () => {
    expect(classifyServer("/a.js", { ok: false, redirected: false }, "")).toBe("unreachable");
  });
  it("Weiterleitung ist same", () => {
    expect(classifyServer("/a.js", { ok: true, redirected: true }, VITE)).toBe("same");
  });
  it("gleicher Einstieg ist same", () => {
    expect(classifyServer("/play/assets/index-BFl2WypM.js", ok, VITE)).toBe("same");
  });
  it("anderer Einstieg ist update", () => {
    expect(classifyServer("/play/assets/index-OLD.js", ok, VITE)).toBe("update");
  });
  it("fehlendes current oder served ist same", () => {
    expect(classifyServer(null, ok, VITE)).toBe("same");
    expect(classifyServer("/a.js", ok, "<html></html>")).toBe("same");
  });
});

class FakeTimers {
  now = 0;
  private nextId = 1;
  private readonly pending = new Map<number, { at: number; handler: () => void }>();
  get count(): number { return this.pending.size; }
  setTimeout(handler: () => void, timeout: number): number {
    const id = this.nextId++;
    this.pending.set(id, { at: this.now + timeout, handler });
    return id;
  }
  clearTimeout(id: number): void { this.pending.delete(id); }
  advance(ms: number): void {
    const target = this.now + ms;
    for (;;) {
      const due = [...this.pending.entries()].filter(([, t]) => t.at <= target).sort((a, b) => a[1].at - b[1].at)[0];
      if (!due) break;
      this.pending.delete(due[0]);
      this.now = due[1].at;
      due[1].handler();
    }
    this.now = target;
  }
}

type Step = "hang" | Error | { ok: boolean; redirected?: boolean; html?: string };

class FakeFetch {
  readonly calls: { url: string; init: RequestInit }[] = [];
  readonly textReads: string[] = [];
  readonly queue: Step[] = [];
  fetch: FetchPage = (url, init) => {
    this.calls.push({ url, init });
    const step = this.queue.shift() ?? { ok: true, html: "" };
    if (step === "hang") {
      return new Promise((_, reject) => {
        init.signal?.addEventListener("abort", () => { reject(new DOMException("aborted", "AbortError")); });
      });
    }
    if (step instanceof Error) return Promise.reject(step);
    return Promise.resolve({
      ok: step.ok,
      redirected: step.redirected ?? false,
      text: () => {
        this.textReads.push(url);
        return Promise.resolve(step.html ?? "");
      },
    });
  };
}

class FakeDocument extends EventTarget {
  visibilityState: DocumentVisibilityState = "visible";
}

const flush = () => new Promise<void>((resolve) => { setTimeout(resolve, 0); });
const OLD = "/play/assets/index-OLD.js";
const NEW_HTML = '<script type="module" src="/play/assets/index-NEW.js"></script>';
const SAME_HTML = `<script type="module" src="${OLD}"></script>`;

function setup(visibility: DocumentVisibilityState = "visible") {
  const timers = new FakeTimers();
  const net = new FakeFetch();
  const doc = new FakeDocument();
  doc.visibilityState = visibility;
  const win = new EventTarget();
  let updates = 0;
  const watch = new ServerWatch(OLD, () => { updates++; }, { fetch: (u, i) => net.fetch(u, i), timers, doc, win });
  return { timers, net, doc, win, watch, updates: () => updates };
}

describe("ServerWatch (AK-17)", () => {
  it("fragt sofort /play ohne Cache ab", async () => {
    const { net, watch } = setup();
    watch.start();
    await flush();
    expect(net.calls).toHaveLength(1);
    expect(net.calls[0]?.url).toBe(PLAY_PATH);
    expect(net.calls[0]?.init.cache).toBe("no-store");
  });

  it("fragt nach unreachable erst nach SERVER_POLL_MS erneut", async () => {
    const { net, timers, watch } = setup();
    net.queue.push({ ok: false });
    watch.start();
    await flush();
    timers.advance(SERVER_POLL_MS - 1);
    await flush();
    expect(net.calls).toHaveLength(1);
    timers.advance(1);
    await flush();
    expect(net.calls).toHaveLength(2);
  });

  it("fragt bei same weiter ab", async () => {
    const { net, timers, watch, updates } = setup();
    net.queue.push({ ok: true, html: SAME_HTML }, { ok: true, html: SAME_HTML });
    watch.start();
    await flush();
    timers.advance(SERVER_POLL_MS);
    await flush();
    timers.advance(SERVER_POLL_MS);
    await flush();
    expect(net.calls).toHaveLength(3);
    expect(updates()).toBe(0);
  });

  it("meldet update genau einmal und reagiert danach auf nichts", async () => {
    const { net, timers, doc, win, watch, updates } = setup();
    net.queue.push({ ok: true, html: NEW_HTML });
    watch.start();
    await flush();
    expect(updates()).toBe(1);
    expect(timers.count).toBe(0);
    doc.dispatchEvent(new Event("visibilitychange"));
    win.dispatchEvent(new Event("online"));
    timers.advance(SERVER_POLL_MS * 3);
    await flush();
    expect(net.calls).toHaveLength(1);
    expect(updates()).toBe(1);
  });

  it("bricht hängende Anfrage nach SERVER_TIMEOUT_MS ab und plant die nächste", async () => {
    const { net, timers, watch } = setup();
    net.queue.push("hang");
    watch.start();
    await flush();
    timers.advance(SERVER_TIMEOUT_MS);
    await flush();
    expect(net.calls[0]?.init.signal?.aborted).toBe(true);
    expect(net.calls).toHaveLength(1);
    timers.advance(SERVER_POLL_MS);
    await flush();
    expect(net.calls).toHaveLength(2);
  });

  it("zählt Netzfehler als unreachable", async () => {
    const { net, timers, watch } = setup();
    net.queue.push(new TypeError("offline"));
    watch.start();
    await flush();
    timers.advance(SERVER_POLL_MS);
    await flush();
    expect(net.calls).toHaveLength(2);
  });

  it("fragt verborgen nicht ab und sofort beim Sichtbarwerden", async () => {
    const { net, doc, watch } = setup("hidden");
    watch.start();
    await flush();
    expect(net.calls).toHaveLength(0);
    doc.visibilityState = "visible";
    doc.dispatchEvent(new Event("visibilitychange"));
    await flush();
    expect(net.calls).toHaveLength(1);
  });

  it("fragt bei online sofort ab und hält danach genau einen Timer", async () => {
    const { net, timers, win, watch } = setup();
    net.queue.push({ ok: false });
    watch.start();
    await flush();
    expect(timers.count).toBe(1);
    win.dispatchEvent(new Event("online"));
    await flush();
    expect(net.calls).toHaveLength(2);
    expect(timers.count).toBe(1);
  });

  it("startet bei online während einer Anfrage keine zweite", async () => {
    const { net, win, watch } = setup();
    net.queue.push("hang");
    watch.start();
    await flush();
    win.dispatchEvent(new Event("online"));
    await flush();
    expect(net.calls).toHaveLength(1);
  });

  it("stop löscht den Timer", async () => {
    const { net, timers, watch } = setup();
    net.queue.push({ ok: false });
    watch.start();
    await flush();
    watch.stop();
    expect(timers.count).toBe(0);
    timers.advance(SERVER_POLL_MS * 2);
    await flush();
    expect(net.calls).toHaveLength(1);
  });

  it("ignoriert eine update-Antwort nach stop", async () => {
    const { net, watch, updates } = setup();
    net.queue.push({ ok: true, html: NEW_HTML });
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => { release = resolve; });
    const inner = net.fetch;
    net.fetch = (u, i) => gate.then(() => inner(u, i));
    watch.start();
    watch.stop();
    release();
    await flush();
    expect(updates()).toBe(0);
  });

  it("liest bei Weiterleitung den Text nicht", async () => {
    const { net, watch } = setup();
    net.queue.push({ ok: true, redirected: true, html: NEW_HTML });
    watch.start();
    await flush();
    expect(net.textReads).toHaveLength(0);
  });

  it("bricht bei stop die laufende Anfrage ab; die neue Sitzung fragt allein", async () => {
    const { net, timers, watch } = setup();
    net.queue.push("hang", "hang");
    watch.start();
    await flush();
    watch.stop();
    expect(net.calls[0]?.init.signal?.aborted).toBe(true);
    await flush();
    expect(timers.count).toBe(0); // Timeout-Timer der alten Anfrage geräumt
    watch.start();
    await flush();
    expect(net.calls).toHaveLength(2);
    expect(net.calls[1]?.init.signal?.aborted).toBe(false);
    timers.advance(SERVER_TIMEOUT_MS);
    await flush();
    expect(timers.count).toBe(1);
  });
});
