import { describe, expect, it, vi } from "vitest";
import { guardLanguageSwitch } from "./language";

interface Field {
  type: string;
  value: string;
}

const MESSAGE = "Wirklich umschalten?";

function run(fields: Field[], answer: boolean, options: { confirm?: string; inside?: Field[] } = {}) {
  const inside = options.inside ?? [];
  const preventDefault = vi.fn();
  const ask = vi.fn(() => answer);
  const switcher = {
    contains: (node: Field) => inside.includes(node),
    dataset: { confirm: "confirm" in options ? options.confirm : MESSAGE },
  };
  guardLanguageSwitch({ preventDefault }, switcher, [...fields, ...inside], ask);
  return { preventDefault, ask };
}

describe("guardLanguageSwitch", () => {
  it("fragt nicht nach, wenn nichts eingegeben ist", () => {
    const { ask, preventDefault } = run([], false);
    expect(ask).not.toHaveBeenCalled();
    expect(preventDefault).not.toHaveBeenCalled();

    const empty = run([{ type: "text", value: "" }, { type: "password", value: "" }], false);
    expect(empty.ask).not.toHaveBeenCalled();
    expect(empty.preventDefault).not.toHaveBeenCalled();
  });

  it("bricht ab, wenn bei Eingaben die Nachfrage verneint wird", () => {
    const { ask, preventDefault } = run([{ type: "text", value: "Tobias" }], false);
    expect(ask).toHaveBeenCalledWith(MESSAGE);
    expect(preventDefault).toHaveBeenCalledTimes(1);
  });

  it("schaltet um, wenn die Nachfrage bestätigt wird", () => {
    const { ask, preventDefault } = run([{ type: "password", value: "geheim" }], true);
    expect(ask).toHaveBeenCalledWith(MESSAGE);
    expect(preventDefault).not.toHaveBeenCalled();
  });

  it("zählt E-Mail-Felder", () => {
    const { ask } = run([{ type: "email", value: "a@example.com" }], true);
    expect(ask).toHaveBeenCalledTimes(1);
  });

  it("ignoriert versteckte Felder", () => {
    const { ask } = run([{ type: "hidden", value: "token" }], false);
    expect(ask).not.toHaveBeenCalled();
  });

  it("ignoriert Felder im Umschalter selbst", () => {
    const { ask } = run([], false, { inside: [{ type: "text", value: "x" }] });
    expect(ask).not.toHaveBeenCalled();
  });

  it("ignoriert Checkboxen und Absende-Buttons mit Wert", () => {
    const { ask } = run([{ type: "checkbox", value: "on" }, { type: "submit", value: "Registrieren" }], false);
    expect(ask).not.toHaveBeenCalled();
  });

  it("fragt nicht nach ohne Nachfragetext", () => {
    const { ask, preventDefault } = run([{ type: "text", value: "Tobias" }], false, { confirm: undefined });
    expect(ask).not.toHaveBeenCalled();
    expect(preventDefault).not.toHaveBeenCalled();
  });
});
