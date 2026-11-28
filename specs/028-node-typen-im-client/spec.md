# 028 – Keine Node-Typen im Browser-Code

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Gehört zu: [017 – Spielfigur als Pixel-Art](../017-spielfigur/spec.md) (T7 bringt `pngjs` in die Client-Tests)

## Ziel

Der Client läuft im Browser. Seine Typprüfung soll Fehler melden, wenn Browser-Code Node-Dinge wie `process` oder
`Buffer` nutzt. Seit 017 T7 kommen über `@types/pngjs` die Node-Typen in das ganze Client-Programm. Code mit `process`
oder `Buffer` besteht die Typprüfung dann und scheitert erst im Browser. Bisher war das bewusst vermieden (Kommentar in
`client/src/features/accounts/api/passkeys.test.ts`). Aufgefallen ist es beim Review von 017 T7.

## User Stories

- Als **Entwickler oder Agent** möchte ich, dass die Typprüfung Node-Dinge im Browser-Code meldet, damit solche Fehler
  nicht erst im Browser auffallen.

## Akzeptanzkriterien

- **AK-1:** Gegeben Browser-Code (kein Test, kein Skript) nutzt `process` oder `Buffer`, dann scheitert die Typprüfung.
- **AK-2:** Gegeben ein Test oder ein Skript nutzt Node-Dinge, die er braucht (z. B. `pngjs` in `assets.test.ts`), dann
  besteht die Typprüfung.

## Out of Scope

- Node-Code im Server oder in Werkzeugen außerhalb von `client/`.

## Offene Fragen

- Getrennte Typprüfung für Tests und Skripte oder ein anderer Weg, die Node-Typen auf sie zu beschränken?
- Nutzt Browser-Code heute schon Node-Typen, ohne dass es auffiel?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
