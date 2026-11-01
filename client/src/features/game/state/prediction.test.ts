import { describe, expect, it } from "vitest";
import { Predictor } from "./prediction";

const STEP = 10;

function initialized(): Predictor {
  const predictor = new Predictor(STEP);
  predictor.reconcile(0, 0, 0);
  return predictor;
}

describe("Predictor", () => {
  it("bewegt die eigene Figur sofort lokal und vergibt fortlaufende Sequenznummern", () => {
    const predictor = initialized();

    const first = predictor.step({ x: 1, y: 0 });
    const second = predictor.step({ x: 1, y: 0 });

    expect([first.seq, second.seq]).toEqual([1, 2]);
    expect(predictor.x).toBe(2 * STEP);
  });

  it("kürzt zu große Richtungen schon vor dem Senden", () => {
    const predictor = initialized();

    const input = predictor.step({ x: 5, y: 0 });

    expect(input.dirX).toBe(1);
    expect(predictor.x).toBe(STEP);
  });

  it("übernimmt die Server-Position und wendet unbestätigte Eingaben erneut an", () => {
    const predictor = initialized();
    for (let i = 0; i < 5; i++) predictor.step({ x: 1, y: 0 });

    predictor.reconcile(20, 0, 2); // Server hat 2 von 5 Eingaben verarbeitet

    expect(predictor.x).toBe(20 + 3 * STEP);
    expect(predictor.pendingCount).toBe(3);
  });

  it("AK-5: eine zu schnelle Vorhersage wird auf die Server-Position zurückgesetzt", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });
    predictor.x = 500; // z. B. manipulierter Client

    predictor.reconcile(STEP, 0, 1);

    expect(predictor.x).toBe(STEP);
    expect(predictor.pendingCount).toBe(0);
  });

  it("interpoliert die Darstellung zwischen zwei Fixed-Steps", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });

    expect(predictor.renderPosition(0.5)).toEqual({ x: STEP / 2, y: 0 });
  });

  it("verschiebt bei einer Korrektur auch die Vorposition (kein Zurückspringen)", () => {
    const predictor = initialized();
    predictor.step({ x: 1, y: 0 });

    predictor.reconcile(100, 0, 0);

    expect(predictor.renderPosition(0)).toEqual({ x: 100, y: 0 });
    expect(predictor.renderPosition(1)).toEqual({ x: 100 + STEP, y: 0 });
  });

  it("verwirft sehr alte unbestätigte Eingaben", () => {
    const predictor = initialized();
    for (let i = 0; i < 150; i++) predictor.step({ x: 0, y: 0 });

    expect(predictor.pendingCount).toBe(100);
  });
});
