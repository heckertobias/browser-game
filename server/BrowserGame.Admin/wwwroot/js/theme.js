// Farbschema des Admins (005 AK-10–12, plan.md „Farbschema“). Läuft synchron im <head> vor dem CSS,
// damit „Dunkel“ beim Laden nicht hell aufblitzt. Umschalter: Elemente mit data-theme-value (light | dark | auto).
(() => {
  "use strict";

  const storageKey = "admin-theme";
  const values = ["light", "dark", "auto"];
  const systemDark = window.matchMedia("(prefers-color-scheme: dark)");

  const load = () => {
    try {
      const value = window.localStorage.getItem(storageKey);
      return values.includes(value) ? value : "auto";
    } catch {
      return "auto";
    }
  };

  const save = (value) => {
    try {
      window.localStorage.setItem(storageKey, value);
    } catch {
      // Ohne Speicher gilt die Wahl nur bis zum Neuladen.
    }
  };

  let current = load();

  const apply = () => {
    const theme = current === "auto" ? (systemDark.matches ? "dark" : "light") : current;
    document.documentElement.setAttribute("data-bs-theme", theme);
    document.querySelectorAll("[data-theme-value]").forEach((item) => {
      const active = item.getAttribute("data-theme-value") === current;
      item.classList.toggle("active", active);
      item.setAttribute("aria-pressed", String(active));
    });
  };

  apply();

  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-theme-value]").forEach((item) => {
      item.addEventListener("click", () => {
        const value = item.getAttribute("data-theme-value");
        if (!values.includes(value)) {
          return;
        }
        current = value;
        save(value);
        apply();
      });
    });
    apply();
  });

  const onSystemChange = () => {
    if (current === "auto") {
      apply();
    }
  };

  if (systemDark.addEventListener) {
    systemDark.addEventListener("change", onSystemChange);
  } else {
    systemDark.addListener(onSystemChange);
  }
})();
