import { describe, expect, it } from "vitest";
import { menuKeyAction } from "./menuLogic";

const key = (over: { key?: string; repeat?: boolean; target?: unknown } = {}) =>
  ({ key: "Escape", repeat: false, target: { tagName: "BODY" }, ...over }) as Parameters<typeof menuKeyAction>[0];

describe("menuKeyAction (002 AK-1)", () => {
  it.each(["BODY", "CANVAS", "BUTTON", "A"])("Esc bei Ziel %s schaltet um", (tagName) => {
    expect(menuKeyAction(key({ target: { tagName } }), false)).toBe("toggle");
  });

  it("Esc ohne Tag am Ziel schaltet um", () => {
    expect(menuKeyAction(key({ target: {} }), false)).toBe("toggle");
  });

  it.each([
    ["andere Taste Enter", { key: "Enter" }],
    ["andere Taste w", { key: "w" }],
    ["repeat", { repeat: true }],
    ["INPUT", { target: { tagName: "INPUT" } }],
    ["TEXTAREA", { target: { tagName: "TEXTAREA" } }],
    ["SELECT", { target: { tagName: "SELECT" } }],
    ["contentEditable", { target: { tagName: "DIV", isContentEditable: true } }],
  ])("nichts bei %s", (_name, over) => {
    expect(menuKeyAction(key(over), false)).toBeNull();
  });

  it("zwei frische Esc-Events schalten beide um", () => {
    expect(menuKeyAction(key(), false)).toBe("toggle");
    expect(menuKeyAction(key(), false)).toBe("toggle");
  });

  describe("beim Aufnehmen", () => {
    it("Esc bricht das Aufnehmen ab", () => {
      expect(menuKeyAction(key(), true)).toBe("cancelRecording");
    });
    it.each([
      ["repeat", { repeat: true }],
      ["INPUT", { target: { tagName: "INPUT" } }],
      ["andere Taste", { key: "w" }],
    ])("nichts bei %s", (_name, over) => {
      expect(menuKeyAction(key(over), true)).toBeNull();
    });
  });
});
