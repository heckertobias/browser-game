import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

describe("Hinweise des Kampfs (013 AK-20)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(["outOfRange", "noTarget"] as const)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });

    it(`alle vier Hinweise in ${language} verschieden`, () => {
      expect(new Set([texts.noTarget, texts.outOfRange, texts.notFacing, texts.notEnoughMana]).size).toBe(4);
    });
  }
});
