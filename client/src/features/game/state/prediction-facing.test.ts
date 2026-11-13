import { describe, expect, it } from "vitest";
import { Predictor } from "@/features/game/state/prediction";

const STEP = 10;
const MIN = 0.01;
const SERVER_DEFAULT = Math.PI / 2;
const deg = (d: number) => (d * Math.PI) / 180;

function initialized(facing = SERVER_DEFAULT): Predictor {
  const predictor = new Predictor(STEP, MIN);
  predictor.reconcile(0, 0, 0, facing);
  return predictor;
}

describe("Predictor – Blickrichtung (016)", () => {
  it("AK-4: übernimmt die Blickrichtung aus dem ersten Snapshot", () => {
    expect(initialized(1.25).facing).toBe(1.25);
  });

  it("AK-1: dreht sofort mit der eigenen Eingabe", () => {
    const predictor = initialized();
    predictor.step({ x: -1, y: 0 });
    expect(predictor.facing).toBe(Math.PI);
  });

  it("AK-2: schräger Winkel bleibt frei", () => {
    const predictor = initialized();
    predictor.step({ x: Math.cos(deg(37)), y: Math.sin(deg(37)) });
    expect(predictor.facing).toBeCloseTo(deg(37));
  });

  it("AK-3: Stillstand behält die letzte Richtung", () => {
    const predictor = initialized();
    predictor.step({ x: 0, y: -1 });
    predictor.step({ x: 0, y: 0 });
    expect(predictor.facing).toBe(-Math.PI / 2);
  });

  it("AK-5: zu kleine Richtung ändert nichts", () => {
    const predictor = initialized(1.25);
    predictor.step({ x: 0.005, y: 0 });
    expect(predictor.facing).toBe(1.25);
  });

  it("zu große Richtung wird gekürzt, der Winkel bleibt", () => {
    const predictor = initialized();
    predictor.step({ x: 5, y: 5 });
    expect(predictor.facing).toBeCloseTo(Math.PI / 4);
  });

  it("übernimmt beim Abgleich den Server-Wert und wendet offene Eingaben erneut an", () => {
    const predictor = initialized(0);
    predictor.step({ x: 1, y: 0 });
    predictor.step({ x: 0, y: 1 });
    predictor.reconcile(STEP, 0, 1, 0);
    expect(predictor.facing).toBe(Math.PI / 2);
  });

  it("ohne offene Eingaben gilt der Server-Wert", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });
    predictor.reconcile(STEP, 0, 1, -0.5);
    expect(predictor.facing).toBe(-0.5);
  });

  it("offener Stillstand ändert den Server-Wert nicht", () => {
    const predictor = initialized();
    predictor.step({ x: 0, y: 0 });
    predictor.reconcile(0, 0, 0, 2);
    expect(predictor.facing).toBe(2);
  });

  it("ohne Server-Wert bleibt die Vorhersage", () => {
    const predictor = initialized(1);
    predictor.step({ x: -1, y: 0 });
    predictor.reconcile(0, 0, 0);
    expect(predictor.facing).toBe(Math.PI);
  });

  it("Schwellwert 0: Stillstand ändert trotzdem nichts", () => {
    const predictor = new Predictor(STEP, 0);
    predictor.reconcile(0, 0, 0, 1);
    predictor.step({ x: 0, y: 0 });
    expect(predictor.facing).toBe(1);
  });
});
