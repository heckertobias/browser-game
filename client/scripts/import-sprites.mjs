// Überführt eine Pack-Figur (Ninja Adventure) in Teile im Sprite-Format (ADR 0018, 017 plan.md Entscheidungen 2, 3, 14).
// Nur für neue Figuren ausführen; das Ergebnis wird eingecheckt und ist danach die Wahrheit:
//   npm run sprites:import -- character --base <A.png> --variant <B.png> --rig src/assets/sprites/humanoid \
//     --body body-default.png --clothing clothing-ninja.png
// A und B sind SpriteSheet.png zweier Farbvarianten derselben Figur. Kleidung sind die Farben von A, die sich an jedem
// ihrer Pixel von B unterscheiden; alles andere ist Körper in den Farben von A.
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { parseArgs } from "node:util";
import { PNG } from "pngjs";

// Aufbau von Actor/Character/<Figur>/SpriteSheet.png (Update #8): Zeile 0 ist zugleich das Standbild.
const PACK_CELL = 16;
const PACK_COLUMNS = ["down", "up", "left", "right"];
const PACK_WALK_ROWS = [0, 1, 2, 3];

const USAGE =
  "Aufruf: npm run sprites:import -- character --base <A.png> --variant <B.png> --rig <Ordner> --body <Datei> --clothing <Datei>";

function fail(message) {
  console.error(`import-sprites: ${message}`);
  process.exit(1);
}

let parsed;
try {
  parsed = parseArgs({
    allowPositionals: true,
    options: {
      base: { type: "string" },
      variant: { type: "string" },
      rig: { type: "string" },
      body: { type: "string" },
      clothing: { type: "string" },
    },
  });
} catch (error) {
  fail(`${error.message}\n${USAGE}`);
}
const { positionals, values } = parsed;
if (positionals.length !== 1 || positionals[0] !== "character") fail(USAGE);
for (const option of ["base", "variant", "rig", "body", "clothing"]) {
  if (values[option] === undefined) fail(USAGE);
}
const { base, variant, rig: rigDir, body: bodyFile, clothing: clothingFile } = values;

const rig = JSON.parse(readFileSync(join(rigDir, "rig.json"), "utf8"));
const rows = Math.max(...Object.values(rig.animations).map((a) => a.firstRow + a.frames));
if (rows > PACK_WALK_ROWS.length) {
  fail(`rig.json braucht ${rows} Zeilen, das Pack hat ${PACK_WALK_ROWS.length} Laufbilder`);
}
const columns = rig.directions.map((direction) => {
  const column = PACK_COLUMNS.indexOf(direction);
  if (column === -1) fail(`Richtung ${direction} gibt es im Pack nicht`);
  return column;
});
const offsetX = rig.origin.x - PACK_CELL / 2;
const offsetY = rig.origin.y - PACK_CELL / 2;
if (
  offsetX < 0 ||
  offsetY < 0 ||
  offsetX + PACK_CELL > rig.cell.width ||
  offsetY + PACK_CELL > rig.cell.height
) {
  fail("Pack-Zelle passt um origin nicht in die Zelle");
}

const a = PNG.sync.read(readFileSync(base));
const b = PNG.sync.read(readFileSync(variant));
if (a.width !== b.width || a.height !== b.height) fail("Basis und Variante sind verschieden groß");
if (a.width < PACK_COLUMNS.length * PACK_CELL || a.height < (Math.max(...PACK_WALK_ROWS) + 1) * PACK_CELL) {
  fail(`Blatt ${a.width}×${a.height} passt nicht zum Pack-Aufbau`);
}

const width = rig.directions.length * rig.cell.width;
const height = rows * rig.cell.height;
const bodyPng = new PNG({ width, height });
const clothingPng = new PNG({ width, height });

const pixels = [];
const colors = new Map();
for (let d = 0; d < rig.directions.length; d++) {
  for (let r = 0; r < rows; r++) {
    for (let y = 0; y < PACK_CELL; y++) {
      for (let x = 0; x < PACK_CELL; x++) {
        const s = ((PACK_WALK_ROWS[r] * PACK_CELL + y) * a.width + columns[d] * PACK_CELL + x) * 4;
        const t = ((r * rig.cell.height + offsetY + y) * width + d * rig.cell.width + offsetX + x) * 4;
        const where = `${rig.directions[d]}, Bild ${r}, Pixel ${x},${y}`;
        const alphaA = a.data[s + 3];
        const alphaB = b.data[s + 3];
        for (const alpha of [alphaA, alphaB]) {
          if (alpha !== 0 && alpha !== 255) fail(`Alpha ${alpha} bei ${where}: nur 0 oder 255 erlaubt`);
        }
        if (alphaA !== alphaB) fail(`Umriss verschieden bei ${where}`);
        // Unsichtbare Pixel zählen nicht, auch wenn ihr RGB abweicht.
        if (alphaA === 0) continue;
        const [red, green, blue] = [a.data[s], a.data[s + 1], a.data[s + 2]];
        const color = `#${[red, green, blue].map((c) => c.toString(16).padStart(2, "0")).join("")}`;
        const differs = red !== b.data[s] || green !== b.data[s + 1] || blue !== b.data[s + 2];
        let entry = colors.get(color);
        if (entry === undefined) {
          entry = { total: 0, differing: 0, l: 0.299 * red + 0.587 * green + 0.114 * blue };
          colors.set(color, entry);
        }
        entry.total++;
        if (differs) entry.differing++;
        pixels.push({ s, t, color });
      }
    }
  }
}

// Eine Farbe, die irgendwo gleich bleibt, gehört zum Körper (z. B. Umriss und Augen), auch wo B abweicht.
const isClothing = (color) => colors.get(color).differing === colors.get(color).total;
const clothingColors = [...colors].filter(([color]) => isClothing(color));
if (clothingColors.length === 0) fail("Maske leer: keine Farbe weicht überall ab");
const maxL = Math.max(...clothingColors.map(([, entry]) => entry.l));
if (maxL === 0) fail("Kleidung ist schwarz, Graustufen nicht ableitbar");

let bodyCount = 0;
let clothingCount = 0;
for (const { s, t, color } of pixels) {
  if (isClothing(color)) {
    const g = Math.round((colors.get(color).l * 255) / maxL);
    clothingPng.data.set([g, g, g, 255], t);
    clothingCount++;
  } else {
    a.data.copy(bodyPng.data, t, s, s + 4);
    bodyCount++;
  }
}

writeFileSync(join(rigDir, bodyFile), PNG.sync.write(bodyPng));
writeFileSync(join(rigDir, clothingFile), PNG.sync.write(clothingPng));

console.log(`Körper: ${bodyCount} Pixel → ${join(rigDir, bodyFile)}`);
console.log(`Kleidung: ${clothingCount} Pixel → ${join(rigDir, clothingFile)}`);
for (const [color, entry] of clothingColors.sort((x, y) => y[1].total - x[1].total)) {
  console.log(`  ${color}: ${entry.total} Pixel, Grau ${Math.round((entry.l * 255) / maxL)}`);
}
const keptDiffering = [...colors].filter(([color, entry]) => !isClothing(color) && entry.differing > 0);
if (keptDiffering.length > 0) {
  console.log("Körper trotz Abweichung (Farbe bleibt anderswo gleich):");
  for (const [color, entry] of keptDiffering.sort((x, y) => y[1].differing - x[1].differing)) {
    console.log(`  ${color}: ${entry.differing} von ${entry.total} Pixeln`);
  }
}
