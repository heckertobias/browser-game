import js from "@eslint/js";
import { defineConfig } from "eslint/config";
import reactHooks from "eslint-plugin-react-hooks";
import globals from "globals";
import tseslint from "typescript-eslint";

// Abhängigkeitsrichtung (docs/project-structure.md): Features kennen weder app/ noch einander;
// erlaubt ist nur `localization`. Querschnitt-Features (localization, pwa) nutzen keine Fach-Features;
// pwa → localization ist erlaubt.
const features = ["accounts", "game", "localization", "pwa"];
const allowedFeatureImports = { game: ["localization"], pwa: ["localization"] };
const generatedOnlyViaAlias = { regex: String.raw`^\.{1,2}/.*\bgenerated/`, message: "generated/ nur über @/generated importieren." };

const featureRestrictions = features.map((feature) => ({
  files: [`src/features/${feature}/**`],
  rules: {
    "no-restricted-imports": [
      "error",
      {
        patterns: [
          generatedOnlyViaAlias,
          { group: ["@/app", "@/app/**", "**/app", "**/app/**"], message: "Features importieren nie aus app/." },
          { group: ["../**"], message: "Über Ordnergrenzen nur per @/-Alias importieren." },
          ...features
            .filter((other) => other !== feature && !(allowedFeatureImports[feature] ?? []).includes(other))
            .map((other) => ({ group: [`@/features/${other}`, `@/features/${other}/**`], message: `Feature ${feature} darf ${other} nicht importieren.` })),
        ],
      },
    ],
  },
}));

export default defineConfig(
  { ignores: ["src/generated/**"] },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  reactHooks.configs.flat.recommended,
  {
    languageOptions: {
      globals: globals.browser,
      parserOptions: {
        projectService: { allowDefaultProject: ["eslint.config.js"] },
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    files: ["src/app/**"],
    rules: { "no-restricted-imports": ["error", { patterns: [generatedOnlyViaAlias] }] },
  },
  ...featureRestrictions,
  { files: ["eslint.config.js"], extends: [tseslint.configs.disableTypeChecked] },
  {
    files: ["scripts/**/*.mjs"],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: globals.node },
  },
);
