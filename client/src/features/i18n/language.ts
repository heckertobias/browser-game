/**
 * Nachfrage vor dem Umschalten der Sprache auf den serverseitigen Spieler-Seiten (006):
 *
 *   <form data-language-switch data-confirm="…Nachfragetext…">…</form>
 *
 * Das Umschalten lädt die Seite neu. Steht in einem Eingabefeld außerhalb des Umschalters schon etwas, fragt der
 * Browser vorher nach; bei Abbruch wird nicht abgeschickt. Ohne JavaScript schaltet der Umschalter ohne Nachfrage um.
 *
 * Keine Imports: sonst entsteht ein gemeinsamer Chunk mit passkeys.js, der im Admin fehlt.
 */

interface Field {
  type: string;
  value: string;
}

const COUNTED = new Set(["text", "email", "password"]);

export function guardLanguageSwitch<F extends Field>(
  event: { preventDefault(): void },
  switcher: { contains(node: F): boolean; dataset: { confirm?: string } },
  fields: Iterable<F>,
  ask: (message: string) => boolean,
): void {
  const message = switcher.dataset.confirm;
  if (!message) {
    return;
  }
  const hasEntries = Array.from(fields).some((f) => COUNTED.has(f.type) && f.value !== "" && !switcher.contains(f));
  if (hasEntries && !ask(message)) {
    event.preventDefault();
  }
}

if (typeof document !== "undefined") {
  document.querySelectorAll<HTMLFormElement>("form[data-language-switch]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      guardLanguageSwitch<HTMLInputElement>(event, form, document.querySelectorAll("input"), (m) => window.confirm(m));
    });
  });
}
