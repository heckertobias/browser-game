import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = ["hp", "mana"] as const;

describe("HP-/Mana-Texte (012 AK-5)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });
  }
});
