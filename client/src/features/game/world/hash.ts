/**
 * Fester 32-Bit-Mix zweier ganzer Zahlen, ohne Zufall und ohne Zustand (018 Entscheidung 6). Ändert sich die Funktion,
 * sieht der Boden anders aus; die festen Werte in groundTiles.test.ts sichern das ab.
 */
export function hash2(a: number, b: number): number {
  let h = Math.imul(a | 0, 0x27d4eb2d) ^ Math.imul(b | 0, 0x165667b1);
  h = Math.imul(h ^ (h >>> 15), 0x85ebca6b);
  h = Math.imul(h ^ (h >>> 13), 0xc2b2ae35);
  return (h ^ (h >>> 16)) >>> 0;
}
