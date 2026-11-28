import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

describe("Startmarkierung (018 AK-7)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it(`startMarker ist in ${language} gesetzt`, () => {
      expect(texts.startMarker.trim()).not.toBe("");
    });
  }
});
