import { describe, expect, it } from "vitest";
import licenses from "./LICENSES.md?raw";

// Register-Format: 017 plan.md „Assets“, ADR 0017.
const COLUMNS = ["Datei", "Herkunft", "Stand", "Lizenz", "Änderungen"];
const ALLOWED_LICENSES = ["CC0 1.0", "eigen"];
const ROW = /^\| `([^`]+)` \|/;

// Pfade relativ zu diesem Ordner, wie in der Spalte „Datei“.
const ASSET_FILE = /\.(png|jpe?g|gif|webp|svg|ttf|otf|woff2?)$/i;
const assetFiles = Object.keys(import.meta.glob("./**/*"))
  .map((key) => key.replace(/^\.\//, ""))
  .filter((file) => ASSET_FILE.test(file));

function cells(line: string): string[] {
  return line
    .split("|")
    .slice(1, -1)
    .map((cell) => cell.trim());
}

function licenseProblems(files: readonly string[], markdown: string): string[] {
  const problems: string[] = [];
  const rows: { file: string; license: string }[] = [];
  for (const line of markdown.split(/\r?\n/)) {
    if (!line.startsWith("| `")) continue;
    const file = ROW.exec(line)?.[1];
    const row = cells(line);
    if (file === undefined || row.length !== COLUMNS.length) {
      problems.push(`ungültige Zeile: ${line}`);
      continue;
    }
    rows.push({ file, license: row[COLUMNS.indexOf("Lizenz")] ?? "" });
  }
  for (const file of files) {
    const matching = rows.filter((row) => row.file === file);
    const [only] = matching;
    if (matching.length !== 1 || only === undefined) problems.push(`${file}: nicht genau eine Zeile in LICENSES.md`);
    else if (!ALLOWED_LICENSES.includes(only.license)) problems.push(`${file}: Lizenz „${only.license}“ nicht erlaubt`);
  }
  for (const row of rows) {
    if (!files.includes(row.file)) problems.push(`${row.file}: Zeile ohne Datei`);
  }
  return problems;
}

const HEADER = "| Datei | Herkunft | Stand | Lizenz | Änderungen |\n|---|---|---|---|---|";

function register(...rows: string[]): string {
  return [HEADER, ...rows].join("\n");
}

function row(file: string, license: string): string {
  return `| \`${file}\` | Quelle | 2026-03-29 | ${license} | keine |`;
}

describe("LICENSES.md (017 AK-16)", () => {
  it("nennt jede Grafik und Schrift im Ordner genau einmal mit erlaubter Lizenz", () => {
    expect(licenseProblems(assetFiles, licenses)).toEqual([]);
  });

  it("hat die Spalten des Registers in fester Reihenfolge", () => {
    const header = licenses.split(/\r?\n/).find((line) => line.startsWith("| Datei |"));
    expect(header).toBeDefined();
    expect(cells(header ?? "")).toEqual(COLUMNS);
  });
});

describe("licenseProblems", () => {
  it("nimmt CC0 1.0 und eigen an", () => {
    const files = ["sprites/a.png", "fonts/b.ttf"];
    expect(licenseProblems(files, register(row("sprites/a.png", "CC0 1.0"), row("fonts/b.ttf", "eigen")))).toEqual([]);
  });

  it("meldet eine Datei ohne Zeile", () => {
    const problems = licenseProblems(["sprites/a.png"], register());
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain("sprites/a.png");
  });

  it("meldet eine Datei mit zwei Zeilen", () => {
    const problems = licenseProblems(["a.png"], register(row("a.png", "CC0 1.0"), row("a.png", "CC0 1.0")));
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain("a.png");
  });

  it("meldet eine nicht erlaubte Lizenz", () => {
    const problems = licenseProblems(["a.png"], register(row("a.png", "CC-BY 4.0")));
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain("CC-BY 4.0");
  });

  it("meldet eine Lizenz in Backticks", () => {
    expect(licenseProblems(["a.png"], register(row("a.png", "`CC0 1.0`")))).toHaveLength(1);
  });

  it("meldet eine Zeile ohne vorhandene Datei", () => {
    const problems = licenseProblems([], register(row("weg.png", "CC0 1.0")));
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain("weg.png");
  });

  it("meldet eine Zeile mit falscher Spaltenzahl", () => {
    expect(licenseProblems([], register("| `a.png` | Quelle | CC0 1.0 |"))).toHaveLength(1);
  });

  it("übergeht Kopf und Trennzeile", () => {
    expect(licenseProblems([], register())).toEqual([]);
  });
});
