import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

const KEYS = ["chatPlaceholder", "chatSend", "chatOpen", "chatCollapse", "chatTooFast", "chatUnread"] as const;

describe("Chat-Texte (011 AK-1, AK-4, AK-13)", () => {
  for (const [language, texts] of Object.entries(dictionaries)) {
    it.each(KEYS)(`%s ist in ${language} gesetzt`, (key) => {
      expect(texts[key].trim()).not.toBe("");
    });
  }
});
