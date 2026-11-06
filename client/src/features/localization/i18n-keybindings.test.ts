import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = [
  "actionMoveUp",
  "actionMoveDown",
  "actionMoveLeft",
  "actionMoveRight",
  "bindingAction",
  "bindingSlot",
  "bindingPressKey",
  "bindingConflict",
  "bindingOverwrite",
  "bindingCancel",
  "bindingBlocked",
  "bindingReserved",
  "bindingReservedChat",
  "bindingClear",
  "bindingReset",
] as const;

describe("Texte der Tastenbelegung (002)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });

    it(`enthält in ${language} die Platzhalter`, () => {
      expect(texts.bindingConflict).toContain("{key}");
      expect(texts.bindingConflict).toContain("{action}");
      expect(texts.bindingBlocked).toContain("{action}");
      expect(texts.bindingSlot).toContain("{n}");
    });

    it(`benennt die Aktionen in ${language} verschieden`, () => {
      const names = [texts.actionMoveUp, texts.actionMoveDown, texts.actionMoveLeft, texts.actionMoveRight];
      expect(new Set(names).size).toBe(names.length);
    });
  }
});
