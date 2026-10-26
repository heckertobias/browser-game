// Erzeugt die Platzhalter-Icons der Web-App (007 AK-10). Nur bei Änderungen ausführen: npm run icons
// Ergebnis ist eingecheckt (server/BrowserGame.Server/wwwroot/icons/), läuft nicht im Build.
import { Buffer } from "node:buffer";
import { mkdirSync, writeFileSync } from "node:fs";
import { crc32, deflateSync } from "node:zlib";

const OUT = new URL("../../server/BrowserGame.Server/wwwroot/icons/", import.meta.url);

// Platzhalter-Motiv: Schild mit Kreuz, 16×16, 2 Pixel Rand (sicher vor Ecken-Masken).
const GRID = [
  "................",
  "................",
  "..############..",
  "..#oooooooooo#..",
  "..#oooo++oooo#..",
  "..#oooo++oooo#..",
  "..#o++++++++o#..",
  "..#o++++++++o#..",
  "..#oooo++oooo#..",
  "...#ooo++ooo#...",
  "...#ooo++ooo#...",
  "....#oo++oo#....",
  ".....#oooo#.....",
  "......####......",
  "................",
  "................",
];
const COLORS = {
  ".": [0x00, 0x00, 0x00],
  "#": [0xfb, 0xbf, 0x24],
  o: [0x1e, 0x40, 0xaf],
  "+": [0xf8, 0xfa, 0xfc],
};

const ICONS = [
  { file: "icon-192.png", size: 192, scale: 12 },
  { file: "icon-512.png", size: 512, scale: 32 },
  { file: "icon-maskable-512.png", size: 512, scale: 24 }, // Motiv im inneren 60 % (Ecken im sicheren Kreis)
  { file: "apple-touch-icon.png", size: 180, scale: 11 }, // 176 px + 2 px Rand, deckend
];

function check() {
  if (GRID.length !== 16) throw new Error("GRID braucht 16 Zeilen.");
  for (const row of GRID) {
    if (row.length !== 16) throw new Error(`Zeile "${row}" braucht 16 Zeichen.`);
    for (const ch of row) {
      if (!(ch in COLORS)) throw new Error(`Unbekanntes Zeichen "${ch}".`);
    }
  }
}

function chunk(type, data) {
  const typeBytes = Buffer.from(type, "ascii");
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBytes, data])) >>> 0);
  return Buffer.concat([length, typeBytes, data, crc]);
}

function encode(size, scale) {
  const offset = Math.floor((size - 16 * scale) / 2);
  const stride = 1 + size * 3;
  const raw = Buffer.alloc(size * stride);
  for (let y = 0; y < size; y++) {
    const gy = Math.floor((y - offset) / scale);
    for (let x = 0; x < size; x++) {
      const gx = Math.floor((x - offset) / scale);
      const inside = gx >= 0 && gx < 16 && gy >= 0 && gy < 16;
      const [r, g, b] = COLORS[inside ? GRID[gy][gx] : "."];
      const i = y * stride + 1 + x * 3;
      raw[i] = r;
      raw[i + 1] = g;
      raw[i + 2] = b;
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;
  ihdr[9] = 2;
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk("IHDR", ihdr),
    chunk("IDAT", deflateSync(raw, { level: 9 })),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

check();
mkdirSync(OUT, { recursive: true });
for (const { file, size, scale } of ICONS) {
  const png = encode(size, scale);
  writeFileSync(new URL(file, OUT), png);
  console.log(file, size, png.length);
}
