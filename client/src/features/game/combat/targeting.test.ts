import { describe, expect, it } from "vitest";
import { TARGET_HIT_RADIUS } from "@/features/game/core/constants";
import { Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { attackRequest, pickTarget, TargetSelection } from "./targeting";

const ORIGIN = { x: 0, y: 0 };

describe("pickTarget (AK-1)", () => {
  it("wählt ein Ziel genau am Weltpunkt", () => {
    expect(pickTarget(ORIGIN, [{ id: "a", x: 0, y: 0 }], TARGET_HIT_RADIUS)).toBe("a");
  });

  it("zählt den Rand zum Treffer", () => {
    expect(pickTarget(ORIGIN, [{ id: "a", x: 12, y: 0 }], TARGET_HIT_RADIUS)).toBe("a");
  });

  it("wählt bei Abstand knapp über dem Radius nichts", () => {
    expect(pickTarget(ORIGIN, [{ id: "a", x: 12.01, y: 0 }], TARGET_HIT_RADIUS)).toBeNull();
  });

  it("wählt bei leerer Liste nichts", () => {
    expect(pickTarget(ORIGIN, [], TARGET_HIT_RADIUS)).toBeNull();
  });

  it("nutzt den übergebenen Radius", () => {
    const targets = [{ id: "a", x: 15, y: 0 }];
    expect(pickTarget(ORIGIN, targets, 20)).toBe("a");
    expect(pickTarget(ORIGIN, targets, 10)).toBeNull();
  });

  it("wählt das nähere Ziel unabhängig von der Reihenfolge (AK-3)", () => {
    const near = { id: "near", x: 3, y: 0 };
    const far = { id: "far", x: 8, y: 0 };
    expect(pickTarget(ORIGIN, [near, far], TARGET_HIT_RADIUS)).toBe("near");
    expect(pickTarget(ORIGIN, [far, near], TARGET_HIT_RADIUS)).toBe("near");
  });

  it("wählt bei gleichem Abstand das erste", () => {
    const targets = [{ id: "a", x: 5, y: 0 }, { id: "b", x: -5, y: 0 }];
    expect(pickTarget(ORIGIN, targets, TARGET_HIT_RADIUS)).toBe("a");
  });
});

describe("TargetSelection (AK-1, AK-3)", () => {
  it("startet ohne Ziel", () => {
    expect(new TargetSelection().current).toBeNull();
  });

  it("merkt sich das gewählte Ziel", () => {
    const selection = new TargetSelection();
    selection.select("a");
    expect(selection.current).toBe("a");
  });

  it("ersetzt das vorherige Ziel", () => {
    const selection = new TargetSelection();
    selection.select("a");
    selection.select("b");
    expect(selection.current).toBe("b");
  });

  it("behält das Ziel, solange es vorhanden ist", () => {
    const selection = new TargetSelection();
    selection.select("a");
    selection.sync(["a"]);
    expect(selection.current).toBe("a");
    selection.sync(["a", "b"]);
    expect(selection.current).toBe("a");
  });

  it.each([[[]], [["b"]]])("hebt die Auswahl auf, wenn das Ziel fehlt (%j)", (ids) => {
    const selection = new TargetSelection();
    selection.select("a");
    selection.sync(ids);
    expect(selection.current).toBeNull();
  });

  it("bleibt ohne Auswahl leer", () => {
    const selection = new TargetSelection();
    selection.sync(["a"]);
    expect(selection.current).toBeNull();
  });

  it("wählt nach dem Aufheben erneut aus", () => {
    const selection = new TargetSelection();
    selection.select("a");
    selection.sync([]);
    selection.select("a");
    expect(selection.current).toBe("a");
  });
});

describe("attackRequest (AK-2)", () => {
  const kinds = [Attack_Kind.MELEE, Attack_Kind.RANGED];

  it.each(kinds)("erzeugt ohne Ziel keinen Angriff (Art %s)", (kind) => {
    expect(attackRequest(new TargetSelection(), kind)).toBeNull();
  });

  it.each(kinds)("zielt mit Auswahl auf das Ziel (Art %s)", (kind) => {
    const selection = new TargetSelection();
    selection.select("dummy");
    const attack = attackRequest(selection, kind);
    expect(attack).not.toBeNull();
    expect(attack?.kind).toBe(kind);
    expect(attack?.targetId).toBe("dummy");
  });

  it("erzeugt nach aufgehobener Auswahl keinen Angriff", () => {
    const selection = new TargetSelection();
    selection.select("dummy");
    selection.sync([]);
    expect(attackRequest(selection, Attack_Kind.MELEE)).toBeNull();
  });
});
