import { defineConfig } from "vite";

/** Fester Build `passkeys.js` für die Razor-Seiten von Spiel und Admin (nicht Teil des Spiel-Clients). */
export default defineConfig({
  publicDir: false,
  build: {
    lib: { entry: "src/account/passkeys.ts", formats: ["es"], fileName: () => "passkeys.js" },
    outDir: "../server/BrowserGame.Server/wwwroot/js",
    emptyOutDir: false,
    minify: true,
  },
});
