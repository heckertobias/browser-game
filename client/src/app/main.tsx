import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { language } from "../features/i18n/i18n";
import { registerServiceWorker } from "../features/pwa/services/pwa";
import "../styles/global.css";

const root = document.getElementById("root");
if (!root) throw new Error("#root fehlt");

document.documentElement.lang = language;

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

registerServiceWorker();
