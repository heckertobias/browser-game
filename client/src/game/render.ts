import { CULL_MARGIN, GRID_MAJOR, GRID_MINOR, PLAYER_SIZE } from "./constants";
import type { Vec } from "./vector";
import type { Viewport } from "./viewport";

export interface DrawablePlayer {
  name: string;
  color: number;
  x: number;
  y: number;
}

const BACKGROUND = "#1d2b1f";
const GRID_MINOR_COLOR = "rgba(255, 255, 255, 0.06)";
const GRID_MAJOR_COLOR = "rgba(255, 255, 255, 0.18)";

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

  ctx.font = "8px monospace";
  ctx.textAlign = "center";
  ctx.textBaseline = "bottom";
  for (const player of players) {
    if (!isVisible(player, origin, viewport, CULL_MARGIN)) continue;
    const left = Math.round(player.x - PLAYER_SIZE / 2);
    const top = Math.round(player.y - PLAYER_SIZE / 2);
    ctx.fillStyle = toCssColor(player.color);
    ctx.fillRect(left, top, PLAYER_SIZE, PLAYER_SIZE);
    ctx.fillStyle = "#ffffff";
    ctx.fillText(player.name, Math.round(player.x), top - 2);
  }
  if (letterbox) ctx.restore();
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
