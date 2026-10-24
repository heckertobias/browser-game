/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  base: "/play/",
  plugins: [react(), tailwindcss()],
  build: {
    outDir: "../server/BrowserGame.Server/wwwroot/play",
    emptyOutDir: true,
  },
  server: {
    // Dev: Vite liefert den Client, der ASP.NET-Server (dotnet run) das Spiel.
    proxy: { "/ws": { target: "ws://localhost:5080", ws: true } },
  },
  test: {
    include: ["src/**/*.test.ts"],
  },
});
