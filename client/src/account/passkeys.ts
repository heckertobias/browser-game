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
 */

type Mode = "create" | "get";

export function toBase64Url(buffer: ArrayBuffer): string {
  let binary = "";
  for (const byte of new Uint8Array(buffer)) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

interface CredentialLike {
  id: string;
  rawId: ArrayBuffer;
  type: string;
  authenticatorAttachment?: string | null;
  response: Record<string, unknown>;
  getClientExtensionResults?: () => unknown;
}

/** Wie `PublicKeyCredential.toJSON`, aber ohne sich darauf zu verlassen (manche Passwortmanager liefern es fehlerhaft). */
export function serializeCredential(credential: CredentialLike): string {
  const response: Record<string, unknown> = {};
  const source = credential.response;
  for (const key of ["clientDataJSON", "attestationObject", "authenticatorData", "signature", "userHandle"]) {
    const value = source[key];
    if (value instanceof ArrayBuffer) response[key] = toBase64Url(value);
  }
  const getTransports = source.getTransports;
  if (typeof getTransports === "function") {
    response.transports = (getTransports as () => string[]).call(source);
  }
  return JSON.stringify({
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? null,
    response,
    clientExtensionResults: credential.getClientExtensionResults?.() ?? {},
  });
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
  form.addEventListener("submit", (event) => {
    if (form.dataset.passkeyReady === "1") return; // zweiter Durchlauf: wirklich absenden
    event.preventDefault();

    if (!("PublicKeyCredential" in window) || typeof PublicKeyCredential.parseCreationOptionsFromJSON !== "function") {
      showError(form, "Dieser Browser unterstützt keine Passkeys. Bitte nutze einen aktuellen Browser.");
      return;
    }

    const optionsElement = form.querySelector("[data-passkey-options]");
    const input = form.querySelector<HTMLInputElement>('input[name="Credential"]');
    if (!optionsElement?.textContent || !input) return;

    void (async () => {
      try {
        const credential = await requestCredential(mode, JSON.parse(optionsElement.textContent));
        if (!credential) {
          showError(form, "Kein Passkey ausgewählt.");
          return;
        }
        input.value = serializeCredential(credential as unknown as CredentialLike);
        form.dataset.passkeyReady = "1";
        form.requestSubmit();
      } catch {
        showError(form, mode === "create"
          ? "Der Passkey konnte nicht angelegt werden (abgebrochen oder nicht erlaubt)."
          : "Die Anmeldung mit Passkey wurde abgebrochen oder ist fehlgeschlagen.");
      }
    })();
  });
}

if (typeof document !== "undefined") {
  document.querySelectorAll<HTMLFormElement>("form[data-passkey]").forEach(attach);
}
