import { describe, expect, it } from "vitest";
import { serializeCredential, toBase64Url } from "./passkeys";

const bytes = (...values: number[]) => new Uint8Array(values).buffer;

describe("toBase64Url", () => {
  it("kodiert ohne Padding und URL-sicher", () => {
    expect(toBase64Url(bytes(0xfb, 0xff, 0xfe))).toBe("-__-");
    expect(toBase64Url(bytes(1))).toBe("AQ");
  });
});

describe("serializeCredential", () => {
  it("serialisiert eine Registrierung wie PublicKeyCredential.toJSON", () => {
    const json = JSON.parse(
      serializeCredential({
        id: "abc",
        rawId: bytes(1, 2, 3),
        type: "public-key",
        authenticatorAttachment: "platform",
        response: { clientDataJSON: bytes(4), attestationObject: bytes(5), getTransports: () => ["internal"] },
        getClientExtensionResults: () => ({ credProps: { rk: true } }),
      }),
    ) as Record<string, unknown>;

    expect(json).toEqual({
      id: "abc",
      rawId: "AQID",
      type: "public-key",
      authenticatorAttachment: "platform",
      response: { clientDataJSON: "BA", attestationObject: "BQ", transports: ["internal"] },
      clientExtensionResults: { credProps: { rk: true } },
    });
  });

  it("serialisiert eine Anmeldung mit Signatur und userHandle", () => {
    const json = JSON.parse(
      serializeCredential({
        id: "x",
        rawId: bytes(9),
        type: "public-key",
        response: { clientDataJSON: bytes(1), authenticatorData: bytes(2), signature: bytes(3), userHandle: bytes(4) },
      }),
    ) as { response: Record<string, string>; authenticatorAttachment: unknown };

    expect(json.response).toEqual({ clientDataJSON: "AQ", authenticatorData: "Ag", signature: "Aw", userHandle: "BA" });
    expect(json.authenticatorAttachment).toBeNull();
  });
});
