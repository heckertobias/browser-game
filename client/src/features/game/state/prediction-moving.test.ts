import { describe, expect, it } from "vitest";
import { Predictor } from "@/features/game/state/prediction";

const STEP = 10;

function initialized(): Predictor {
  const predictor = new Predictor(STEP);
  predictor.reconcile(0, 0, 0);
  return predictor;
}

describe("Predictor.moving (017 AK-7, AK-8)", () => {
  it("steht vor dem ersten Snapshot", () => {
    expect(new Predictor(STEP).moving).toBe(false);
  });

  it("steht nach dem ersten Snapshot ohne Schritt", () => {
    expect(initialized().moving).toBe(false);
  });

  it("AK-7: läuft nach einem Schritt mit Richtung", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });
    expect(predictor.moving).toBe(true);
    predictor.step({ x: 0, y: 1 });
    expect(predictor.moving).toBe(true);
  });

  it("AK-8: steht nach einem Schritt ohne Richtung und behält die Blickrichtung", () => {
    const predictor = initialized();
    predictor.step({ x: 0, y: 1 });
    predictor.step({ x: 0, y: 0 });
    expect(predictor.moving).toBe(false);
    expect(predictor.facing).toBeCloseTo(Math.PI / 2);
  });

  it("eine Server-Korrektur allein lässt die Figur nicht laufen", () => {
    const predictor = initialized();
    predictor.step({ x: 0, y: 0 });
    predictor.reconcile(50, 0, 1);
    expect(predictor.x).toBe(50);
    expect(predictor.moving).toBe(false);
  });

  it("ein laufender Schritt bleibt nach einer Korrektur laufend", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });
    predictor.reconcile(100, 0, 0);
    expect(predictor.moving).toBe(true);
  });

  it("ein großer Sprung der Server-Korrektur lässt eine stehende Figur nicht laufen", () => {
    const from = 910.9122076639474;
    const to = 39.11621750528216;
    const predictor = new Predictor(STEP);
    predictor.reconcile(from, 0, 0);
    predictor.step({ x: 0, y: 0 });
    predictor.reconcile(to, 0, 1);
    expect(predictor.x).toBe(to);
    expect(predictor.moving).toBe(false);
  });
});
