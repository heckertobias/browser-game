import { describe, expect, it } from "vitest";
import { detectLanguage, dictionaries } from "./i18n";

const COOKIE = ".AspNetCore.Culture";

describe("detectLanguage", () => {
  it("nimmt das Cookie vor der Browsersprache (en)", () => {
    expect(detectLanguage(`${COOKIE}=c%3Den%7Cuic%3Den`, "de-DE")).toBe("en");
  });

  it("nimmt das Cookie vor der Browsersprache (de)", () => {
    expect(detectLanguage(`${COOKIE}=c%3Dde%7Cuic%3Dde`, "en-US")).toBe("de");
  });

  it("findet das Cookie zwischen anderen Cookies", () => {
    expect(detectLanguage(`foo=1; ${COOKIE}=c%3Dde%7Cuic%3Dde; bar=2`, "en")).toBe("de");
  });

  it("bevorzugt uic vor c", () => {
    expect(detectLanguage(`${COOKIE}=c%3Den%7Cuic%3Dde`, "en")).toBe("de");
  });

  it("liest auch ein Cookie nur mit c", () => {
    expect(detectLanguage(`${COOKIE}=c%3Dde`, "en")).toBe("de");
  });

  it("liest ein unkodiertes Cookie", () => {
    expect(detectLanguage(`${COOKIE}=c=de|uic=de`, "en")).toBe("de");
  });

  it("ignoriert Region und Groß-/Kleinschreibung im Cookie", () => {
    expect(detectLanguage(`${COOKIE}=uic%3Dde-AT`, "en")).toBe("de");
    expect(detectLanguage(`${COOKIE}=uic%3DDE`, "en")).toBe("de");
  });

  it("nimmt ohne Cookie die Browsersprache", () => {
    for (const browser of ["de-DE", "de-AT", "DE"]) expect(detectLanguage("", browser)).toBe("de");
    for (const browser of ["en-US", "fr-FR", ""]) expect(detectLanguage("", browser)).toBe("en");
  });

  it("fällt bei unbekannter Cookie-Sprache auf den Browser zurück", () => {
    expect(detectLanguage(`${COOKIE}=uic%3Dfr`, "de-DE")).toBe("de");
  });

  it("erkennt nur den genauen Cookie-Namen", () => {
    expect(detectLanguage(`x${COOKIE}=uic%3Dde`, "en")).toBe("en");
  });

  it("wirft bei kaputter Kodierung nicht und nimmt den Browser", () => {
    expect(detectLanguage(`${COOKIE}=%E0%A4%A`, "de")).toBe("de");
  });

  it("nimmt Englisch, wenn alles fehlt", () => {
    expect(detectLanguage("", "")).toBe("en");
  });
});

describe("Wörterbücher", () => {
  it("enthalten die Texte der Offline-Seite in beiden Sprachen (007 AK-12)", () => {
    for (const texts of Object.values(dictionaries)) {
      for (const key of ["offlineTitle", "offlineMessage", "offlineRetry"] as const) {
        expect(texts[key].trim()).not.toBe("");
      }
    }
  });
});
