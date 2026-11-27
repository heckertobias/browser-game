import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = ["commandUnknown", "commandInvalid"] as const;

describe("Befehls-Texte (015 AK-4)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });

    it(`die beiden Hinweise unterscheiden sich in ${language}`, () => {
      expect(texts.commandUnknown).not.toBe(texts.commandInvalid);
    });
  }
});
