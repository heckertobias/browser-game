import { describe, expect, it } from "vitest";
import { dictionaries } from "./i18n";

describe("Texte für Spiel-Overlay und HUD (003)", () => {
  it("enthalten kicked-Overlay und Account-Link in beiden Sprachen (AK-12)", () => {
    for (const dictionary of Object.values(dictionaries)) {
      expect(dictionary.kickedSignedInElsewhere.trim()).not.toBe("");
      expect(dictionary.kickedContinueHere.trim()).not.toBe("");
      expect(dictionary.myAccount.trim()).not.toBe("");
    }
  });

  it("nennen den Hinweis aus der Spec (AK-12)", () => {
    expect(dictionaries.de.kickedSignedInElsewhere).toBe("Auf einem anderen Gerät angemeldet");
    expect(dictionaries.de.kickedContinueHere).toBe("Hier weiterspielen");
    expect(dictionaries.de.myAccount).toBe("Mein Account");
  });
});
