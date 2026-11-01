/**
 * Passkey-Formulare für die serverseitigen Account-Seiten (Spiel und Admin, ADR 0005):
 *
 *   <form data-passkey="create|get">
 *     <script type="application/json" data-passkey-options>…WebAuthn-Optionen vom Server…</script>
 *     <input type="hidden" name="Credential">
 *     <p data-passkey-error hidden></p>
 *   </form>
 *
 * Beim Absenden fragt der Browser den Passkey ab; das Ergebnis landet als JSON im versteckten Feld und das Formular
 * wird normal abgeschickt. Keine JSON-API nötig.
 *
 * Fehlertexte nach `<html lang>`. Bewusst ohne Imports: sonst entsteht ein gemeinsamer Chunk, der im Admin fehlt.
 */

type Mode = "create" | "get";

const en = {
  unsupported: "This browser does not support passkeys. Please use a current browser.",
  noneSelected: "No passkey selected.",
  createFailed: "The passkey could not be created (cancelled or not allowed).",
  getFailed: "The passkey was not confirmed (cancelled or failed).",
} as const;

const de: Record<keyof typeof en, string> = {
  unsupported: "Dieser Browser unterstützt keine Passkeys. Bitte nutze einen aktuellen Browser.",
  noneSelected: "Kein Passkey ausgewählt.",
  createFailed: "Der Passkey konnte nicht angelegt werden (abgebrochen oder nicht erlaubt).",
  getFailed: "Der Passkey wurde nicht bestätigt (abgebrochen oder fehlgeschlagen).",
};

type Binary = ArrayBuffer | ArrayBufferView;

/**
 * Bytes erkennen – auch aus einem anderen JavaScript-Kontext (Passwortmanager-Erweiterungen liefern Credentials oft
 * aus ihrer eigenen „Welt“, dann schlägt `instanceof ArrayBuffer` fehl).
 */
export function isBinary(value: unknown): value is Binary {
  return value instanceof ArrayBuffer
    || ArrayBuffer.isView(value)
    || Object.prototype.toString.call(value) === "[object ArrayBuffer]";
}

export function toBase64Url(value: Binary): string {
  const bytes = ArrayBuffer.isView(value)
    ? new Uint8Array(value.buffer, value.byteOffset, value.byteLength)
    : new Uint8Array(value);
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

interface CredentialLike {
  id: string;
  rawId: unknown;
  type: string;
  authenticatorAttachment?: string | null;
  response: Record<string, unknown>;
  getClientExtensionResults?: () => unknown;
  toJSON?: () => unknown;
}

/** Eigene Serialisierung im Format von `PublicKeyCredential.toJSON` (WebAuthn Level 3). */
export function serializeCredential(credential: CredentialLike): string {
  const response: Record<string, unknown> = {};
  const source = credential.response;
  for (const key of ["clientDataJSON", "attestationObject", "authenticatorData", "signature", "userHandle"]) {
    const value = source[key];
    if (isBinary(value)) response[key] = toBase64Url(value);
  }
  const call = (name: string): unknown => {
    const fn = source[name];
    return typeof fn === "function" ? (fn as () => unknown).call(source) : undefined;
  };
  const transports = call("getTransports");
  if (Array.isArray(transports)) response.transports = transports;
  if (!("authenticatorData" in response)) {
    const authenticatorData = call("getAuthenticatorData");
    if (isBinary(authenticatorData)) response.authenticatorData = toBase64Url(authenticatorData);
  }
  const publicKeyAlgorithm = call("getPublicKeyAlgorithm");
  if (typeof publicKeyAlgorithm === "number") response.publicKeyAlgorithm = publicKeyAlgorithm;
  const publicKey = call("getPublicKey");
  if (isBinary(publicKey)) response.publicKey = toBase64Url(publicKey);

  return JSON.stringify({
    id: credential.id,
    rawId: isBinary(credential.rawId) ? toBase64Url(credential.rawId) : credential.id,
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? null,
    response,
    clientExtensionResults: credential.getClientExtensionResults?.() ?? {},
  });
}

/**
 * Bevorzugt das browsereigene `toJSON()`; nur wenn es fehlt oder offensichtlich unbrauchbar ist (manche
 * Passwortmanager liefern es fehlerhaft), greift die eigene Serialisierung.
 */
export function credentialToJson(credential: CredentialLike): string {
  if (typeof credential.toJSON === "function") {
    try {
      const json = credential.toJSON() as { id?: unknown; response?: { clientDataJSON?: unknown } } | null;
      if (json && typeof json.id === "string" && typeof json.response?.clientDataJSON === "string") {
        return JSON.stringify(json);
      }
    } catch {
      // fällt auf die eigene Serialisierung zurück
    }
  }
  return serializeCredential(credential);
}

function showError(form: HTMLFormElement, message: string): void {
  const element = form.querySelector<HTMLElement>("[data-passkey-error]");
  if (element) {
    element.textContent = message;
    element.hidden = false;
  }
}

async function requestCredential(mode: Mode, optionsJson: unknown): Promise<Credential | null> {
  if (mode === "create") {
    const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson as PublicKeyCredentialCreationOptionsJSON);
    return navigator.credentials.create({ publicKey });
  }
  const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson as PublicKeyCredentialRequestOptionsJSON);
  return navigator.credentials.get({ publicKey });
}

function attach(form: HTMLFormElement): void {
  const mode = form.dataset.passkey as Mode;
  const texts = document.documentElement.lang.toLowerCase().startsWith("de") ? de : en;
  form.addEventListener("submit", (event) => {
    if (form.dataset.passkeyReady === "1") return; // zweiter Durchlauf: wirklich absenden
    event.preventDefault();

    if (!("PublicKeyCredential" in window) || typeof PublicKeyCredential.parseCreationOptionsFromJSON !== "function") {
      showError(form, texts.unsupported);
      return;
    }

    const optionsElement = form.querySelector("[data-passkey-options]");
    const input = form.querySelector<HTMLInputElement>('input[name="Credential"]');
    if (!optionsElement?.textContent || !input) return;

    void (async () => {
      try {
        const credential = await requestCredential(mode, JSON.parse(optionsElement.textContent));
        if (!credential) {
          showError(form, texts.noneSelected);
          return;
        }
        input.value = credentialToJson(credential as unknown as CredentialLike);
        form.dataset.passkeyReady = "1";
        form.requestSubmit();
      } catch {
        showError(form, mode === "create" ? texts.createFailed : texts.getFailed);
      }
    })();
  });
}

if (typeof document !== "undefined") {
  document.querySelectorAll<HTMLFormElement>("form[data-passkey]").forEach(attach);
}
