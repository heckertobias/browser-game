/** Sichtfeld (ADR 0010): kurze Seite möglichst nah an 360 Weltpixeln, nie mehr als 400; lange Seite höchstens 21:9. */
export const TARGET_SHORT_SIDE = 360;
export const MAX_SHORT_SIDE = 400;
export const MAX_ASPECT_LONG = 21;
export const MAX_ASPECT_SHORT = 9;

export const PLAYER_SIZE = 16;
/** Pfeil der Blickrichtung (016) in Weltpixeln: Abstand vom Rand der Figur, Länge der Spitze, Breite der Basis. */
export const ARROW_GAP = 3;
export const ARROW_LENGTH = 5;
export const ARROW_WIDTH = 6;
/** Culling (ADR 0010): Figur samt Namen (≤ 16 Zeichen) muss das Sichtfeld um diesen Rand in Weltpixeln schneiden. */
export const CULL_MARGIN = 64;
export const GRID_MINOR = 32;
export const GRID_MAJOR = 256;

/** Andere Spieler werden so weit in der Vergangenheit dargestellt, um zwischen Snapshots zu interpolieren. */
export const INTERPOLATION_DELAY_MS = 100;

/** Halten-und-Folgen: So nah am Zeiger bleibt die Figur stehen (verhindert Zittern). */
export const POINTER_DEAD_ZONE = 4;

/** Zielauswahl (013): Tippen/Klicken höchstens so weit vom Mittelpunkt eines Ziels wählt es aus. Bedienung, kein Spielwert. */
export const TARGET_HIT_RADIUS = 12;

/** Trainings-Dummy (013) in Weltpixeln: Pfosten mit Querbalken, Position = Mitte des Pfostens. */
export const DUMMY_POST_WIDTH = 4;
export const DUMMY_HEIGHT = 20;
export const DUMMY_ARM_WIDTH = 14;
export const DUMMY_ARM_HEIGHT = 4;
/** Abstand des Querbalkens von der Oberkante des Pfostens. */
export const DUMMY_ARM_OFFSET = 5;
/** HP-Balken über einem Ziel (013 AK-18): Breite, Höhe, Abstand über der Oberkante des Ziels. */
export const HP_BAR_WIDTH = 20;
export const HP_BAR_HEIGHT = 3;
export const HP_BAR_GAP = 4;
/** Umrandung des aktiven Ziels (013 AK-1): Abstand um das Ziel und Strichstärke in Weltpixeln. */
export const TARGET_OUTLINE_GAP = 2;
export const TARGET_OUTLINE_WIDTH = 1;
/** Projektil (013): Radius des Kreises in Weltpixeln. */
export const PROJECTILE_RADIUS = 2;
