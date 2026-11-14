import {
  ARROW_GAP,
  ARROW_LENGTH,
  ARROW_WIDTH,
  CULL_MARGIN,
  DUMMY_ARM_HEIGHT,
  DUMMY_ARM_OFFSET,
  DUMMY_ARM_WIDTH,
  DUMMY_HEIGHT,
  DUMMY_POST_WIDTH,
  GRID_MAJOR,
  GRID_MINOR,
  HP_BAR_GAP,
  HP_BAR_HEIGHT,
  HP_BAR_WIDTH,
  PLAYER_SIZE,
  PROJECTILE_RADIUS,
  TARGET_OUTLINE_GAP,
  TARGET_OUTLINE_WIDTH,
} from "@/features/game/core/constants";
import type { Vec } from "@/features/game/core/vector";
import type { Viewport } from "./viewport";

export interface DrawablePlayer {
  name: string;
  color: number;
  x: number;
  y: number;
  /** Verbindung verloren, Figur wartet noch (AK-20). */
  away?: boolean;
  /** Blickrichtung in Bogenmaß (016); ohne Wert kein Pfeil. */
  facing?: number;
}

/** Zielbares Objekt (013); `TargetState` aus dem Snapshot passt direkt. */
export interface DrawableTarget {
  id: string;
  x: number;
  y: number;
  hp: number;
  maxHp: number;
}

/** Kampf im Bild (013): Ziele ohne Interpolation, Projektile interpoliert, höchstens ein aktives Ziel. */
export interface CombatScene {
  targets: readonly DrawableTarget[];
  projectiles: readonly Vec[];
  selectedId: string | null;
}

const EMPTY_SCENE: CombatScene = { targets: [], projectiles: [], selectedId: null };

/** Gefüllte Breite des HP-Balkens (013 AK-18): anteilig, auf 0 … `width` begrenzt; ohne Maximum leer. */
export function hpBarWidth(hp: number, maxHp: number, width: number): number {
  if (maxHp <= 0) return 0;
  return width * Math.min(1, Math.max(0, hp / maxHp));
}

const AWAY_ALPHA = 0.5;

/** Grauwert gleicher Helligkeit (Rec. 601) für wartende Figuren (AK-20). */
export function desaturate(rgb: number): number {
  const r = (rgb >> 16) & 0xff;
  const g = (rgb >> 8) & 0xff;
  const b = rgb & 0xff;
  const l = Math.round(0.299 * r + 0.587 * g + 0.114 * b);
  return (l << 16) | (l << 8) | l;
}

const BACKGROUND = "#1d2b1f";
const GRID_MINOR_COLOR = "rgba(255, 255, 255, 0.06)";
const GRID_MAJOR_COLOR = "rgba(255, 255, 255, 0.18)";
const ARROW_FILL = "#ffffff";
const ARROW_OUTLINE = "#000000";
const DUMMY_COLOR = "#8b5a2b";
const HP_BAR_BACKGROUND = "#000000";
const HP_BAR_FILL = "#e53935";
const TARGET_OUTLINE_COLOR = "#ffd400";
const PROJECTILE_COLOR = "#fff6c2";
/** Sichtbarer Rand des Pfeils in Weltpixeln (016 AK-6). */
const ARROW_OUTLINE_WIDTH = 1;

/**
 * Ecken des Blickrichtungs-Pfeils um den Mittelpunkt (x, y): Spitze, dann die beiden Ecken der Basis (016 AK-6).
 * Die Basis liegt `PLAYER_SIZE / 2 + ARROW_GAP` vor dem Mittelpunkt; der Winkel wird nicht gerastert (AK-7).
 */
export function arrowPoints(x: number, y: number, facing: number): readonly [Vec, Vec, Vec] {
  const dx = Math.cos(facing);
  const dy = Math.sin(facing);
  const base = PLAYER_SIZE / 2 + ARROW_GAP;
  const reach = base + ARROW_LENGTH;
  const half = ARROW_WIDTH / 2;
  return [
    { x: x + dx * reach, y: y + dy * reach },
    { x: x + dx * base - dy * half, y: y + dy * base + dx * half },
    { x: x + dx * base + dy * half, y: y + dy * base - dx * half },
  ];
}

/** Weltkoordinate der linken oberen Ecke des Sichtfelds, auf Gerätepixel gerundet (kein Flimmern). */
export function cameraOrigin(camera: Vec, viewport: Viewport): Vec {
  const { scale, viewWidth, viewHeight } = viewport;
  return {
    x: Math.round((camera.x - viewWidth / 2) * scale) / scale,
    y: Math.round((camera.y - viewHeight / 2) * scale) / scale,
  };
}

/** Liegt die Figur samt Namen (Rand `margin` in Weltpixeln) im Sichtfeld? */
export function isVisible(player: Vec, origin: Vec, viewport: Viewport, margin: number): boolean {
  return (
    player.x >= origin.x - margin &&
    player.x <= origin.x + viewport.viewWidth + margin &&
    player.y >= origin.y - margin &&
    player.y <= origin.y + viewport.viewHeight + margin
  );
}

/** CSS-Pixel relativ zum Canvas → Weltkoordinaten; berücksichtigt Gerätepixel, Balken (offset) und k. */
export function screenToWorld(
  x: number,
  y: number,
  cssWidth: number,
  cssHeight: number,
  viewport: Viewport,
  origin: Vec,
): Vec {
  const { scale, canvasWidth, canvasHeight, offsetX, offsetY } = viewport;
  return {
    x: origin.x + ((x * canvasWidth) / cssWidth - offsetX) / scale,
    y: origin.y + ((y * canvasHeight) / cssHeight - offsetY) / scale,
  };
}

export function render(
  ctx: CanvasRenderingContext2D,
  viewport: Viewport,
  camera: Vec,
  players: readonly DrawablePlayer[],
  scene: CombatScene = EMPTY_SCENE,
): void {
  const { scale, canvasWidth, canvasHeight, viewWidth, viewHeight, offsetX, offsetY } = viewport;
  const origin = cameraOrigin(camera, viewport);

  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.imageSmoothingEnabled = false;
  const letterbox = offsetX > 0 || offsetY > 0;
  if (letterbox) {
    ctx.save(); // sonst schneidet sich der Clip Frame für Frame weiter
    ctx.fillStyle = "#000000";
    ctx.fillRect(0, 0, canvasWidth, canvasHeight);
    ctx.beginPath();
    ctx.rect(offsetX, offsetY, Math.round(viewWidth * scale), Math.round(viewHeight * scale));
    ctx.clip();
  }
  ctx.setTransform(scale, 0, 0, scale, offsetX - origin.x * scale, offsetY - origin.y * scale);
  ctx.fillStyle = BACKGROUND;
  ctx.fillRect(origin.x, origin.y, viewWidth, viewHeight);

  drawGrid(ctx, origin, viewWidth, viewHeight, GRID_MINOR, 1 / scale, GRID_MINOR_COLOR);
  drawGrid(ctx, origin, viewWidth, viewHeight, GRID_MAJOR, 1, GRID_MAJOR_COLOR);

  for (const target of scene.targets) {
    if (!isVisible(target, origin, viewport, CULL_MARGIN)) continue;
    drawTarget(ctx, target, target.id === scene.selectedId);
  }

  ctx.font = "8px monospace";
  ctx.textAlign = "center";
  ctx.textBaseline = "bottom";
  for (const player of players) {
    if (!isVisible(player, origin, viewport, CULL_MARGIN)) continue;
    const left = Math.round(player.x - PLAYER_SIZE / 2);
    const top = Math.round(player.y - PLAYER_SIZE / 2);
    ctx.globalAlpha = player.away ? AWAY_ALPHA : 1;
    ctx.fillStyle = toCssColor(player.away ? desaturate(player.color) : player.color);
    ctx.fillRect(left, top, PLAYER_SIZE, PLAYER_SIZE);
    if (player.facing !== undefined) drawArrow(ctx, left + PLAYER_SIZE / 2, top + PLAYER_SIZE / 2, player.facing);
    ctx.fillStyle = "#ffffff";
    ctx.fillText(player.name, Math.round(player.x), top - 2);
  }
  ctx.globalAlpha = 1;
  for (const projectile of scene.projectiles) {
    if (!isVisible(projectile, origin, viewport, CULL_MARGIN)) continue;
    drawProjectile(ctx, projectile);
  }
  if (letterbox) ctx.restore();
}

/** Pfosten mit Querbalken, darüber der HP-Balken; das aktive Ziel bekommt eine Umrandung (013 AK-1, AK-18). */
function drawTarget(ctx: CanvasRenderingContext2D, target: DrawableTarget, selected: boolean): void {
  const cx = Math.round(target.x);
  const top = Math.round(target.y) - DUMMY_HEIGHT / 2;
  ctx.fillStyle = DUMMY_COLOR;
  ctx.fillRect(cx - DUMMY_POST_WIDTH / 2, top, DUMMY_POST_WIDTH, DUMMY_HEIGHT);
  ctx.fillRect(cx - DUMMY_ARM_WIDTH / 2, top + DUMMY_ARM_OFFSET, DUMMY_ARM_WIDTH, DUMMY_ARM_HEIGHT);
  if (selected) drawOutline(ctx, cx - DUMMY_ARM_WIDTH / 2, top, DUMMY_ARM_WIDTH, DUMMY_HEIGHT);
  const barLeft = cx - HP_BAR_WIDTH / 2;
  const barTop = top - HP_BAR_GAP - HP_BAR_HEIGHT;
  ctx.fillStyle = HP_BAR_BACKGROUND;
  ctx.fillRect(barLeft, barTop, HP_BAR_WIDTH, HP_BAR_HEIGHT);
  ctx.fillStyle = HP_BAR_FILL;
  ctx.fillRect(barLeft, barTop, hpBarWidth(target.hp, target.maxHp, HP_BAR_WIDTH), HP_BAR_HEIGHT);
}

/** Rahmen `TARGET_OUTLINE_GAP` außerhalb des Kastens; der Pfad liegt eine halbe Strichstärke innen, damit ganze Pixel gefüllt werden. */
function drawOutline(ctx: CanvasRenderingContext2D, left: number, top: number, width: number, height: number): void {
  const inset = TARGET_OUTLINE_WIDTH / 2;
  const l = left - TARGET_OUTLINE_GAP + inset;
  const t = top - TARGET_OUTLINE_GAP + inset;
  const r = left + width + TARGET_OUTLINE_GAP - inset;
  const b = top + height + TARGET_OUTLINE_GAP - inset;
  ctx.beginPath();
  ctx.moveTo(l, t);
  ctx.lineTo(r, t);
  ctx.lineTo(r, b);
  ctx.lineTo(l, b);
  ctx.closePath();
  ctx.strokeStyle = TARGET_OUTLINE_COLOR;
  ctx.lineWidth = TARGET_OUTLINE_WIDTH;
  ctx.lineJoin = "miter";
  ctx.stroke();
}

function drawProjectile(ctx: CanvasRenderingContext2D, projectile: Vec): void {
  ctx.fillStyle = PROJECTILE_COLOR;
  ctx.beginPath();
  ctx.arc(projectile.x, projectile.y, PROJECTILE_RADIUS, 0, 2 * Math.PI);
  ctx.fill();
}

/** Dunkler Rand zuerst, die Füllung deckt seine innere Hälfte (016 AK-6). */
function drawArrow(ctx: CanvasRenderingContext2D, x: number, y: number, facing: number): void {
  const [tip, baseA, baseB] = arrowPoints(x, y, facing);
  ctx.beginPath();
  ctx.moveTo(tip.x, tip.y);
  ctx.lineTo(baseA.x, baseA.y);
  ctx.lineTo(baseB.x, baseB.y);
  ctx.closePath();
  ctx.strokeStyle = ARROW_OUTLINE;
  ctx.lineWidth = 2 * ARROW_OUTLINE_WIDTH;
  ctx.lineJoin = "round";
  ctx.stroke();
  ctx.fillStyle = ARROW_FILL;
  ctx.fill();
}

function drawGrid(
  ctx: CanvasRenderingContext2D,
  origin: Vec,
  width: number,
  height: number,
  spacing: number,
  thickness: number,
  color: string,
): void {
  ctx.fillStyle = color;
  for (let x = Math.floor(origin.x / spacing) * spacing; x <= origin.x + width; x += spacing) {
    ctx.fillRect(x, origin.y, thickness, height);
  }
  for (let y = Math.floor(origin.y / spacing) * spacing; y <= origin.y + height; y += spacing) {
    ctx.fillRect(origin.x, y, width, thickness);
  }
}

export function toCssColor(rgb: number): string {
  return `#${rgb.toString(16).padStart(6, "0")}`;
}
