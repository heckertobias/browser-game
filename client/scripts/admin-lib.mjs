// Kopiert Bootstrap und Bootstrap Icons ins Admin (ADR 0007, 005). Ausgabe ist gitignored, läuft in build:admin.
import { cpSync, mkdirSync, rmSync } from "node:fs";

const modules = new URL("../node_modules/", import.meta.url);
const out = new URL("../../server/BrowserGame.Admin/wwwroot/lib/", import.meta.url);

const files = [
  ["bootstrap/dist/css/bootstrap.min.css", "bootstrap/bootstrap.min.css"],
  ["bootstrap/dist/js/bootstrap.bundle.min.js", "bootstrap/bootstrap.bundle.min.js"],
  ["bootstrap-icons/font/bootstrap-icons.min.css", "bootstrap-icons/bootstrap-icons.min.css"],
  ["bootstrap-icons/font/fonts", "bootstrap-icons/fonts"],
];

rmSync(out, { recursive: true, force: true });
for (const [from, to] of files) {
  const target = new URL(to, out);
  mkdirSync(new URL(".", target), { recursive: true });
  cpSync(new URL(from, modules), target, { recursive: true });
}
