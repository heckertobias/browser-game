import { GRID_MAJOR, GRID_MINOR, LOGICAL_HEIGHT, LOGICAL_WIDTH, PLAYER_SIZE } from "./constants";
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
export function cameraOrigin(camera: Vec, scale: number): Vec {
  return {
    x: Math.round((camera.x - LOGICAL_WIDTH / 2) * scale) / scale,
    y: Math.round((camera.y - LOGICAL_HEIGHT / 2) * scale) / scale,
  };
}

export function render(
  ctx: CanvasRenderingContext2D,
  viewport: Viewport,
  camera: Vec,
  players: readonly DrawablePlayer[],
): void {
  const { scale } = viewport;
  const origin = cameraOrigin(camera, scale);

  ctx.setTransform(scale, 0, 0, scale, -origin.x * scale, -origin.y * scale);
  ctx.imageSmoothingEnabled = false;
  ctx.fillStyle = BACKGROUND;
  ctx.fillRect(origin.x, origin.y, LOGICAL_WIDTH, LOGICAL_HEIGHT);

  drawGrid(ctx, origin, GRID_MINOR, 1 / scale, GRID_MINOR_COLOR);
  drawGrid(ctx, origin, GRID_MAJOR, 1, GRID_MAJOR_COLOR);

  ctx.font = "8px monospace";
  ctx.textAlign = "center";
  ctx.textBaseline = "bottom";
  for (const player of players) {
    const left = Math.round(player.x - PLAYER_SIZE / 2);
    const top = Math.round(player.y - PLAYER_SIZE / 2);
    ctx.fillStyle = toCssColor(player.color);
    ctx.fillRect(left, top, PLAYER_SIZE, PLAYER_SIZE);
    ctx.fillStyle = "#ffffff";
    ctx.fillText(player.name, Math.round(player.x), top - 2);
  }
}

function drawGrid(ctx: CanvasRenderingContext2D, origin: Vec, spacing: number, thickness: number, color: string): void {
  ctx.fillStyle = color;
  for (let x = Math.floor(origin.x / spacing) * spacing; x <= origin.x + LOGICAL_WIDTH; x += spacing) {
    ctx.fillRect(x, origin.y, thickness, LOGICAL_HEIGHT);
  }
  for (let y = Math.floor(origin.y / spacing) * spacing; y <= origin.y + LOGICAL_HEIGHT; y += spacing) {
    ctx.fillRect(origin.x, y, LOGICAL_WIDTH, thickness);
  }
}

export function toCssColor(rgb: number): string {
  return `#${rgb.toString(16).padStart(6, "0")}`;
}
