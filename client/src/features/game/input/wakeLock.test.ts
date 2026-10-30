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

describe("ScreenWakeLock (AK-14)", () => {
  it("requests on start when visible", async () => {
    const { api, lock } = setup();
    lock.start();
    await flush();
    expect(api.types).toEqual(["screen"]);
  });

  it("does not request while hidden", async () => {
    const { api, lock, fire, show } = setup("hidden");
    lock.start();
    fire("visibilitychange");
    await flush();
    expect(api.requests).toBe(0);
    show();
    await flush();
    expect(api.requests).toBe(1);
  });

  it("requests again when visible again", async () => {
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

  it("does not request twice", async () => {
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

  it("releases on stop and stops listening", async () => {
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

  it("releases a lock that arrives after stop", async () => {
    const { api, lock } = setup();
    api.mode = "manual";
    lock.start();
    lock.stop();
    api.resolveOpen();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
  });

  it("handles start twice and stop/start", async () => {
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

  it("does nothing without API", async () => {
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

  it("stays silent on rejection and retries on every input until it gets the lock", async () => {
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

  it("requests on touchend, not on touch pointerup", async () => {
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

  it("one request per tap while pending", () => {
    const { api, doc, lock, fire } = setup();
    api.mode = "manual";
    lock.start();
    doc.dispatchEvent(Object.assign(new Event("pointerup"), { pointerType: "touch" }));
    fire("touchend");
    fire("keydown");
    expect(api.requests).toBe(1);
  });

  it("requests again on input after the lock was released without visibilitychange", async () => {
    const { api, lock, fire } = setup();
    lock.start();
    await flush();
    api.sentinel(0).released = true;
    fire("pointerup");
    await flush();
    expect(api.requests).toBe(2);
  });

  it("ignores repeated keydown", async () => {
    const { api, doc, lock } = setup();
    api.mode = "deny";
    lock.start();
    await flush();
    doc.dispatchEvent(Object.assign(new Event("keydown"), { repeat: true }));
    await flush();
    expect(api.requests).toBe(1);
  });

  it("gets the lock on input after a rejection", async () => {
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

  it("stays silent when release fails", async () => {
    const { api, lock } = setup();
    lock.start();
    await flush();
    api.sentinel(0).failRelease = true;
    lock.stop();
    await flush();
    expect(api.sentinel(0).releaseCalls).toBe(1);
  });
});
