import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = ["attackMelee", "attackRanged", "notEnoughMana", "notFacing", "actionAttackMelee", "actionAttackRanged"] as const;

describe("Texte des Kampfs (013)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });

    it(`Hinweise und Buttons in ${language} unterscheidbar`, () => {
      expect(texts.notEnoughMana).not.toBe(texts.notFacing);
      expect(texts.attackMelee).not.toBe(texts.attackRanged);
    });

    it(`alle Aktionen in ${language} verschieden benannt`, () => {
      const names = [
        texts.actionMoveUp,
        texts.actionMoveDown,
        texts.actionMoveLeft,
        texts.actionMoveRight,
        texts.actionAttackMelee,
        texts.actionAttackRanged,
      ];
      expect(new Set(names).size).toBe(6);
    });
  }
});
