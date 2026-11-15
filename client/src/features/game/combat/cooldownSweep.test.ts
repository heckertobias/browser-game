import { describe, expect, it, vi } from "vitest";
import { CooldownSweep, cooldownRemaining, sweepAngle, sweepBackground } from "./cooldownSweep";

describe("cooldownRemaining (013 AK-22)", () => {
  it("läuft von 1 auf 0", () => {
    expect(cooldownRemaining(100, 100, 1000)).toBe(1);
    expect(cooldownRemaining(350, 100, 1000)).toBe(0.75);
    expect(cooldownRemaining(600, 100, 1000)).toBe(0.5);
    expect(cooldownRemaining(1100, 100, 1000)).toBe(0);
    expect(cooldownRemaining(2000, 100, 1000)).toBe(0);
    expect(cooldownRemaining(50, 100, 1000)).toBe(1);
  });

  it("ohne gültige Dauer kein Rest", () => {
    for (const duration of [0, -1, Number.NaN]) {
      expect(cooldownRemaining(100, 0, duration)).toBe(0);
    }
  });
});

describe("sweepAngle (013 AK-22)", () => {
  it("wächst im Uhrzeigersinn", () => {
    expect(sweepAngle(1)).toBe(0);
    expect(sweepAngle(0.75)).toBe(90);
    expect(sweepAngle(0.5)).toBe(180);
    expect(sweepAngle(0)).toBe(360);
    expect(sweepAngle(1.5)).toBe(0);
    expect(sweepAngle(-0.5)).toBe(360);
  });
});

describe("sweepBackground (013 AK-22)", () => {
  it("ist frei bis zum Winkel, danach dunkel, ab 12 Uhr", () => {
    for (const remaining of [1, 0.5, 0.25]) {
      const css = sweepBackground(remaining);
      expect(css.startsWith("conic-gradient(")).toBe(true);
      expect(css).not.toContain("from");
      const match = /^conic-gradient\(transparent ([\d.]+)deg, (.+) ([\d.]+)deg\)$/.exec(css);
      expect(match).not.toBeNull();
      const angle = sweepAngle(remaining).toFixed(1);
      expect(match?.[1]).toBe(angle);
      expect(match?.[3]).toBe(angle);
      expect(match?.[2]).not.toBe("transparent");
    }
  });
});

function fake() {
  let clock = 0;
  const frames: ((time: number) => void)[] = [];
  const changes: number[] = [];
  const cancelFrame = vi.fn<(handle: number) => void>();
  const sweep = new CooldownSweep(
    (remaining) => { changes.push(remaining); },
    () => clock,
    (callback) => frames.push(callback),
    cancelFrame,
  );
  return { sweep, frames, changes, cancelFrame, setClock: (value: number) => { clock = value; } };
}

describe("CooldownSweep (013 AK-22)", () => {
  it("läuft über die gemeldete Dauer ab", () => {
    const f = fake();
    f.setClock(100);
    f.sweep.start(1000);
    expect(f.changes).toEqual([1]);
    expect(f.frames).toHaveLength(1);

    f.frames[0]?.(350);
    expect(f.changes.at(-1)).toBe(0.75);
    expect(f.frames).toHaveLength(2);
    f.frames[1]?.(600);
    expect(f.changes.at(-1)).toBe(0.5);
    expect(f.frames).toHaveLength(3);
    f.frames[2]?.(1100);
    expect(f.changes.at(-1)).toBe(0);
    expect(f.frames).toHaveLength(3);
  });

  it("ein neuer Start setzt zurück", () => {
    const f = fake();
    f.setClock(0);
    f.sweep.start(1000);
    f.frames[0]?.(500);
    expect(f.changes.at(-1)).toBe(0.5);

    f.setClock(600);
    f.sweep.start(1000);
    expect(f.changes.at(-1)).toBe(1);
    expect(f.cancelFrame).toHaveBeenCalledWith(2);
    f.frames[2]?.(1100);
    expect(f.changes.at(-1)).toBe(0.5);
  });

  it("eine ungültige Dauer startet nichts", () => {
    const f = fake();
    for (const duration of [0, -1, Number.NaN]) f.sweep.start(duration);
    expect(f.changes).toEqual([]);
    expect(f.frames).toEqual([]);
  });

  it("clear stoppt", () => {
    const f = fake();
    f.sweep.start(1000);
    f.sweep.clear();
    expect(f.cancelFrame).toHaveBeenCalledTimes(1);
    expect(() => { f.sweep.clear(); }).not.toThrow();
    expect(f.cancelFrame).toHaveBeenCalledTimes(1);
  });
});
