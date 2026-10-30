import { language, t } from "../../i18n/i18n";
import { browserReloadWindow, reloadPage } from "../services/reload";
import "../../../styles/global.css";

function element(id: string): HTMLElement {
  const found = document.getElementById(id);
  if (!found) throw new Error(`#${id} fehlt`);
  return found;
}

// Offline-Seite (007 AK-12): Sprache aus Culture-Cookie bzw. Browser, beides offline vorhanden.
document.documentElement.lang = language;
element("offline-title").textContent = t.offlineTitle;
element("offline-message").textContent = t.offlineMessage;
const retry = element("offline-retry");
retry.textContent = t.offlineRetry;
const hint = element("offline-hint");
// Die Seite steht unter der ursprünglichen URL (z. B. /play); Neuladen versucht es dort erneut.
retry.addEventListener("click", () => {
  retry.setAttribute("disabled", "");
  retry.textContent = t.reloading;
  reloadPage(browserReloadWindow(), () => {
    retry.removeAttribute("disabled");
    retry.textContent = t.offlineRetry;
    hint.textContent = t.reloadStuck;
    hint.hidden = false;
  });
});
