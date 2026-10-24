/** Logische Auflösung: Jeder sieht genau diesen Weltausschnitt (ADR 0003). */
export const LOGICAL_WIDTH = 640;
export const LOGICAL_HEIGHT = 360;

export const PLAYER_SIZE = 16;
export const GRID_MINOR = 32;
export const GRID_MAJOR = 256;

/** Andere Spieler werden so weit in der Vergangenheit dargestellt, um zwischen Snapshots zu interpolieren. */
export const INTERPOLATION_DELAY_MS = 100;

/** Halten-und-Folgen: So nah am Zeiger bleibt die Figur stehen (verhindert Zittern). */
export const POINTER_DEAD_ZONE = 4;
