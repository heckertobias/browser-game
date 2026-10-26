/** Sichtfeld (ADR 0010): kurze Seite möglichst nah an 360 Weltpixeln, nie mehr als 400; lange Seite höchstens 21:9. */
export const TARGET_SHORT_SIDE = 360;
export const MAX_SHORT_SIDE = 400;
export const MAX_ASPECT_LONG = 21;
export const MAX_ASPECT_SHORT = 9;

export const PLAYER_SIZE = 16;
export const GRID_MINOR = 32;
export const GRID_MAJOR = 256;

/** Andere Spieler werden so weit in der Vergangenheit dargestellt, um zwischen Snapshots zu interpolieren. */
export const INTERPOLATION_DELAY_MS = 100;

/** Halten-und-Folgen: So nah am Zeiger bleibt die Figur stehen (verhindert Zittern). */
export const POINTER_DEAD_ZONE = 4;
