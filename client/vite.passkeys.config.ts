import { defineConfig } from "vite";

/**
 * Feste Builds `passkeys.js` und `language.js` für die Razor-Seiten von Spiel und Admin (nicht Teil des Spiel-Clients).
 * Die Einträge dürfen keine gemeinsamen Module importieren.
 */
export default defineConfig({
  publicDir: false,
  build: {
    lib: {
      entry: { passkeys: "src/account/passkeys.ts", language: "src/account/language.ts" },
      formats: ["es"],
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: "../server/BrowserGame.Server/wwwroot/js",
    emptyOutDir: false,
    minify: true,
  },
});
