import { describe, expect, it } from "vitest";
import { ScreenWakeLock, type WakeLockApi } from "./wakeLock";

class FakeSentinel {
  released = false;
  releaseCalls = 0;
  failRelease = false;
  release(): Promise<void> {
    this.releaseCalls++;
    this.released = true;
    return this.failRelease ? Promise.reject(new DOMException("released", "InvalidStateError")) : Promise.resolve();
  }
}

class FakeWakeLock implements WakeLockApi {
  mode: "grant" | "deny" | "manual" = "grant";
  readonly types: string[] = [];
  readonly sentinels: FakeSentinel[] = [];
  private readonly open: (() => void)[] = [];
  get requests(): number { return this.types.length; }
  request(type: "screen"): Promise<FakeSentinel> {
    this.types.push(type);
    if (this.mode === "deny") return Promise.reject(new DOMException("denied", "NotAllowedError"));
    const sentinel = new FakeSentinel();
    this.sentinels.push(sentinel);
    if (this.mode === "grant") return Promise.resolve(sentinel);
    return new Promise((resolve) => { this.open.push(() => { resolve(sentinel); }); });
  }
  resolveOpen(): void { for (const resolve of this.open.splice(0)) resolve(); }
  sentinel(index: number): FakeSentinel {
    const sentinel = this.sentinels[index];
    if (!sentinel) throw new Error(`no sentinel ${String(index)}`);
    return sentinel;
  }
}

class FakeDocument extends EventTarget {
  visibilityState: DocumentVisibilityState = "visible";
}

const flush = () => new Promise<void>((resolve) => { setTimeout(resolve, 0); });

function setup(visibility: DocumentVisibilityState = "visible") {
  const api = new FakeWakeLock();
  const doc = new FakeDocument();
  doc.visibilityState = visibility;
  const lock = new ScreenWakeLock(api, doc);
  const fire = (type: string) => { doc.dispatchEvent(new Event(type)); };
  const hide = () => {
    doc.visibilityState = "hidden";
    for (const s of api.sentinels) s.released = true;
    fire("visibilitychange");
  };
  const show = () => { doc.visibilityState = "visible"; fire("visibilitychange"); };
  return { api, doc, lock, fire, hide, show };
}

describe("ScreenWakeLock (007 AK-14)", () => {
  it("fordert beim Start an, wenn die Seite sichtbar ist", async () => {
    const { api, lock } = setup();
    lock.start();
    await flush();
    expect(api.types).toEqual(["screen"]);
  });

  it("fordert nicht an, solange die Seite verborgen ist", async () => {
    const { api, lock, fire, show } = setup("hidden");
    lock.start();
    fire("visibilitychange");
    await flush();
    expect(api.requests).toBe(0);
    show();
    await flush();
    expect(api.requests).toBe(1);
  });

  it("fordert erneut an, sobald die Seite wieder sichtbar ist (der Browser gibt die Sperre beim Verstecken frei)", async () => {
    const { api, lock, hide, show } = setup();
    lock.start();
    await flush();
    hide();
    await flush();
    expect(api.requests).toBe(1);
    show();
    await flush();
    expect(api.requests).toBe(2);
  });

  it("fordert nicht doppelt an", async () => {
    const { api, lock, fire, show } = setup();
    api.mode = "manual";
    lock.start();
    show();
    fire("pointerup");
    fire("keydown");
    expect(api.requests).toBe(1);
    api.resolveOpen();
    await flush();
    show();
    fire("pointerup");
    expect(api.requests).toBe(1);
  });

  it("gibt die Sperre bei stop frei und hört auf zu lauschen", async () => {
    const { api, lock, fire, hide, show } = setup();
    lock.start();
    await flush();
    lock.stop();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
    hide();
    show();
    fire("pointerup");
    await flush();
    expect(api.requests).toBe(1);
  });

  it("gibt eine Sperre frei, die erst nach stop eintrifft", async () => {
    const { api, lock } = setup();
    api.mode = "manual";
    lock.start();
    lock.stop();
    api.resolveOpen();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
  });

  it("verträgt doppeltes start sowie stop und erneutes start", async () => {
    const { api, lock, hide, show } = setup();
    lock.start();
    lock.start();
    await flush();
    expect(api.requests).toBe(1);
    hide();
    show();
    await flush();
    expect(api.requests).toBe(2);
    lock.stop();
    lock.start();
    await flush();
    expect(api.requests).toBe(3);
    expect(api.sentinel(2).releaseCalls).toBe(0);
  });

  it("tut ohne Wake-Lock-API nichts", async () => {
    const doc = new FakeDocument();
    const lock = new ScreenWakeLock(undefined, doc);
    expect(() => {
      lock.start();
      doc.dispatchEvent(new Event("visibilitychange"));
      doc.dispatchEvent(new Event("pointerup"));
      lock.stop();
    }).not.toThrow();
    await flush();
  });

  // Manche Browser gewähren die Sperre erst nach einer Nutzergeste; darum zählt jede Eingabe als neuer Versuch.
  it("bleibt bei Ablehnung still und versucht es bei jeder Eingabe erneut, bis die Sperre gewährt ist", async () => {
    const { api, lock, fire, show } = setup();
    api.mode = "deny";
    lock.start();
    await flush();
    expect(api.requests).toBe(1);
    fire("pointerup");
    await flush();
    expect(api.requests).toBe(2);
    fire("keydown");
    await flush();
    expect(api.requests).toBe(3);
    fire("pointerup");
    await flush();
    expect(api.requests).toBe(4);
    api.mode = "grant";
    fire("keydown");
    await flush();
    expect(api.requests).toBe(5);
    fire("pointerup");
    fire("keydown");
    show();
    await flush();
    expect(api.requests).toBe(5);
  });

  it("fordert bei Touch mit touchend an, nicht schon beim pointerup derselben Berührung", async () => {
    const { api, doc, lock, fire } = setup();
    api.mode = "deny";
    lock.start();
    await flush();
    doc.dispatchEvent(Object.assign(new Event("pointerup"), { pointerType: "touch" }));
    await flush();
    expect(api.requests).toBe(1);
    fire("touchend");
    await flush();
    expect(api.requests).toBe(2);
  });

  it("höchstens eine Anfrage je Tippen, solange eine läuft", () => {
    const { api, doc, lock, fire } = setup();
    api.mode = "manual";
    lock.start();
    doc.dispatchEvent(Object.assign(new Event("pointerup"), { pointerType: "touch" }));
    fire("touchend");
    fire("keydown");
    expect(api.requests).toBe(1);
  });

  it("fordert bei der nächsten Eingabe erneut an, wenn der Browser die Sperre ohne visibilitychange freigegeben hat", async () => {
    const { api, lock, fire } = setup();
    lock.start();
    await flush();
    api.sentinel(0).released = true;
    fire("pointerup");
    await flush();
    expect(api.requests).toBe(2);
  });

  it("ignoriert wiederholtes keydown einer gehaltenen Taste", async () => {
    const { api, doc, lock } = setup();
    api.mode = "deny";
    lock.start();
    await flush();
    doc.dispatchEvent(Object.assign(new Event("keydown"), { repeat: true }));
    await flush();
    expect(api.requests).toBe(1);
  });

  it("bekommt die Sperre bei einer Eingabe nach einer Ablehnung", async () => {
    const { api, lock, fire } = setup();
    api.mode = "deny";
    lock.start();
    await flush();
    api.mode = "grant";
    fire("pointerup");
    await flush();
    lock.stop();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
  });

  it("bleibt still, wenn die Freigabe scheitert", async () => {
    const { api, lock } = setup();
    lock.start();
    await flush();
    api.sentinel(0).failRelease = true;
    lock.stop();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
  });
});
