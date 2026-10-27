import { describe, expect, it } from "vitest";
import { RELOAD_STUCK_MS, reloadPage, type ReloadWindow } from "./reload";

class FakeWindow implements ReloadWindow {
  reloads = 0;
  readonly timers: { handler: () => void; timeout: number }[] = [];
  readonly location = {
    reload: () => {
      this.reloads++;
    },
  };
  setTimeout(handler: () => void, timeout: number): number {
    this.timers.push({ handler, timeout });
    return this.timers.length;
  }
}

describe("reloadPage (007 AK-16)", () => {
  it("lädt sofort genau einmal neu", () => {
    const win = new FakeWindow();
    reloadPage(win, () => undefined);
    expect(win.reloads).toBe(1);
  });

  it("plant den Hängt-Timer mit RELOAD_STUCK_MS", () => {
    const win = new FakeWindow();
    reloadPage(win, () => undefined);
    expect(win.timers.map((t) => t.timeout)).toEqual([RELOAD_STUCK_MS]);
  });

  it("ruft onStuck erst bei Ablauf und genau einmal", () => {
    const win = new FakeWindow();
    let stuck = 0;
    reloadPage(win, () => {
      stuck++;
    });
    expect(stuck).toBe(0);
    win.timers[0]?.handler();
    expect(stuck).toBe(1);
  });
});
