import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = ["menu", "menuContinue", "menuKeyBindings", "menuSignOut", "back", "myAccount"] as const;

describe("Menü-Texte (002 AK-3)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });
  }
});
