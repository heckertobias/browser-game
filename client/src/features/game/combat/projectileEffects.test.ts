import { describe, expect, it } from "vitest";
import { ProjectileEffects, type EffectTarget } from "./projectileEffects";

const dummy = (hp = 100): EffectTarget => ({ id: "dummy", x: 0, y: -100, hp, maxHp: 100 });
const hpOf = (effects: ProjectileEffects) => effects.targets().map((t) => t.hp);
const ORIGIN = { x: 0, y: 0 };
const TO = { x: 0, y: -100 };

function flying(): ProjectileEffects {
  const effects = new ProjectileEffects(1000);
  effects.observe([dummy()]);
  effects.launch(0, 1, ORIGIN, TO, 400);
  return effects;
}

describe("ProjectileEffects: Flug (013 AK-21)", () => {
  it("fliegt geradlinig vom Start zum Ziel", () => {
    const effects = flying();
    expect(effects.positions(0)).toEqual([{ x: 0, y: 0 }]);
    expect(effects.positions(125)).toEqual([{ x: 0, y: -50 }]);
    effects.update(250);
    expect(effects.positions(250)).toEqual([]);
  });

  it("Dauer ist auf maxMs begrenzt", () => {
    const effects = new ProjectileEffects(1000);
    effects.launch(0, 1, ORIGIN, TO, 1);
    effects.update(999);
    expect(effects.positions(999)).toHaveLength(1);
    effects.update(1000);
    expect(effects.positions(1000)).toEqual([]);
  });

  it.each([0, -1, Number.NaN])("ohne gültiges Tempo (%s) kein Effekt; ein Treffer wirkt sofort", (speed) => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy()]);
    effects.launch(0, 1, ORIGIN, TO, speed);
    expect(effects.positions(0)).toEqual([]);
    effects.hit(0, 1, "dummy", 15);
    expect(hpOf(effects)).toEqual([85]);
  });
});

describe("ProjectileEffects: zurückgehaltene Treffer (013 AK-7, AK-18, AK-21)", () => {
  it("HP sinken erst beim Einschlag", () => {
    const effects = flying();
    effects.hit(200, 1, "dummy", 15);
    expect(hpOf(effects)).toEqual([100]);
    effects.observe([dummy(85)]);
    expect(hpOf(effects)).toEqual([100]);
    effects.update(249);
    expect(hpOf(effects)).toEqual([100]);
    effects.update(250);
    expect(hpOf(effects)).toEqual([85]);
  });

  it("ein Treffer nach dem Einschlag wirkt sofort", () => {
    const effects = flying();
    effects.update(250);
    effects.hit(260, 1, "dummy", 15);
    expect(hpOf(effects)).toEqual([85]);
  });

  it("ein Treffer ohne bekannten Start wirkt sofort", () => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy()]);
    effects.hit(0, 9, "dummy", 15);
    expect(hpOf(effects)).toEqual([85]);
  });

  it("Nahkampf (Snapshot ohne Ereignis) wirkt sofort, auch neben einem zurückgehaltenen Treffer", () => {
    const effects = flying();
    effects.hit(100, 1, "dummy", 15);
    effects.observe([dummy(75)]);
    expect(hpOf(effects)).toEqual([90]);
  });

  it("mehrere Treffer werden einzeln freigegeben", () => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy()]);
    effects.launch(0, 1, ORIGIN, { x: 0, y: -100 }, 400);
    effects.launch(0, 2, ORIGIN, { x: 0, y: -200 }, 400);
    effects.hit(100, 1, "dummy", 15);
    effects.hit(100, 2, "dummy", 10);
    effects.observe([dummy(75)]);
    expect(hpOf(effects)).toEqual([100]);
    effects.update(250);
    expect(hpOf(effects)).toEqual([85]);
    effects.update(500);
    expect(hpOf(effects)).toEqual([75]);
  });
});

describe("ProjectileEffects: Ziel verschwindet (013 AK-15, AK-21)", () => {
  it("tödlicher Treffer: das Ziel bleibt bis zum Einschlag sichtbar", () => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy(15)]);
    effects.launch(0, 1, ORIGIN, TO, 400);
    effects.hit(100, 1, "dummy", 15);
    effects.observe([]);
    expect(effects.targets()).toEqual([dummy(15)]);
    effects.update(250);
    expect(effects.targets()).toEqual([]);
  });

  it("ein fehlendes Ziel ohne zurückgehaltenen Treffer verschwindet sofort", () => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy()]);
    effects.observe([]);
    expect(effects.targets()).toEqual([]);
  });

  it("Respawn beendet den Ghost; die Anzeige ist auf Max-HP begrenzt", () => {
    const effects = new ProjectileEffects(1000);
    effects.observe([dummy(15)]);
    effects.launch(0, 1, ORIGIN, TO, 400);
    effects.hit(100, 1, "dummy", 15);
    effects.observe([]);
    effects.observe([dummy(100)]);
    expect(effects.targets()).toEqual([dummy(100)]);
  });

  it("clear vergisst alles", () => {
    const effects = flying();
    effects.hit(100, 1, "dummy", 15);
    effects.clear();
    expect(effects.positions(100)).toEqual([]);
    expect(effects.targets()).toEqual([]);
  });
});
