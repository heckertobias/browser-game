import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { INTERPOLATION_DELAY_MS, PLAYER_SIZE } from "@/features/game/core/constants";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { FakeContext } from "@/features/game/render/fakeContext";
import type { Pixels } from "@/features/game/sprites/compose";
import { spriteDirection } from "@/features/game/sprites/direction";
import { frameRect, parseRig, sheetSize } from "@/features/game/sprites/rig";
import { SpriteSet } from "@/features/game/sprites/spriteSet";
import { blank } from "@/features/game/sprites/testPixels";
import { Game } from "./game";

let frame: FrameRequestCallback | undefined;
let now = 0;

beforeEach(() => {
  frame = undefined;
  now = 0;
  FakeWebSocket.instances.length = 0;
  vi.stubGlobal("WebSocket", FakeWebSocket);
  vi.stubGlobal("ResizeObserver", class {
    constructor(private readonly callback: (entries: unknown[]) => void) {}
    observe() {
      this.callback([{ devicePixelContentBoxSize: [{ inlineSize: 640, blockSize: 360 }] }]);
    }
    disconnect() {}
  });
  vi.stubGlobal("window", Object.assign(new EventTarget(), {
    location: { protocol: "http:", host: "test" },
    devicePixelRatio: 1,
  }));
  vi.stubGlobal("document", Object.assign(new EventTarget(), { hidden: false, visibilityState: "visible" }));
  vi.stubGlobal("performance", { now: () => now });
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    frame = callback;
    return 1;
  });
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const rig = parseRig({
  cell: { width: 48, height: 40 },
  origin: { x: 24, y: 20 },
  directions: ["down", "up", "left", "right"],
  animations: { idle: { firstRow: 0, frames: 1 }, walk: { firstRow: 1, frames: 4, frameMs: 100 } },
  slots: ["body", "clothing"],
  parts: {
    "body/default": { slot: "body", image: "body.png" },
    "clothing/ninja": { slot: "clothing", image: "clothing.png", tint: true },
  },
});

function spriteSet(): SpriteSet {
  const size = sheetSize(rig);
  const parts = new Map<string, Pixels>([
    ["body/default", blank(size.width, size.height)],
    ["clothing/ninja", blank(size.width, size.height)],
  ]);
  const toSource = vi.fn((pixels: Pixels) => ({ pixels }) as unknown as CanvasImageSource);
  return new SpriteSet(rig, parts, toSource);
}

function setup() {
  const ctx = new FakeContext();
  const canvas = Object.assign(new EventTarget(), {
    getContext: () => ctx.asContext(),
    width: 0,
    height: 0,
  }) as unknown as HTMLCanvasElement;
  const game = new Game(canvas, {
    onStatus: () => {},
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
  });
  game.start();
  return { ctx, game, socket: FakeWebSocket.latest() };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01 },
});

const toggle = serverBytes({ case: "toggleFacingArrows", value: {} });

const snapshot = (bx: number, ownFacing = 1.25) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      players: [
        { id: "a", name: "n", x: 0, y: 0, facing: ownFacing },
        { id: "b", name: "o", x: bx, y: 20, facing: -2 },
      ],
    },
  });

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

const squares = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === PLAYER_SIZE && r.height === PLAYER_SIZE);
const press = (code: string) => window.dispatchEvent(Object.assign(new Event("keydown"), { code }));
const release = (code: string) => window.dispatchEvent(Object.assign(new Event("keyup"), { code }));

function expectCell(image: { sx: number; sy: number } | undefined, expected: { sx: number; sy: number }): void {
  expect(image).toBeDefined();
  expect({ sx: image?.sx, sy: image?.sy }).toEqual(expected);
}

describe("Game: Figuren als Sprite (017)", () => {
  it("AK-14: ohne setSprites Quadrate", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(40));
    drawFrame(ctx);
    expect(ctx.images()).toHaveLength(0);
    expect(squares(ctx)).toHaveLength(2);
  });

  it("AK-1, AK-14: mit setSprites je Figur ein Bild, kein Quadrat, kein Pfeil", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    drawFrame(ctx);
    expect(ctx.images()).toHaveLength(2);
    expect(squares(ctx)).toHaveLength(0);
    expect(ctx.paths("fill")).toHaveLength(0);
  });

  it("AK-14: setSprites(null) fällt auf Quadrate zurück", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    game.setSprites(null);
    socket.receive(welcome);
    socket.receive(snapshot(40));
    drawFrame(ctx);
    expect(ctx.images()).toHaveLength(0);
    expect(squares(ctx)).toHaveLength(2);
  });

  it("AK-11: Pfeil auch mit Sprites", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    socket.receive(toggle);
    drawFrame(ctx);
    expect(ctx.paths("fill")).toHaveLength(2);
    expect(ctx.images()).toHaveLength(2);
  });

  it("AK-6: erste eigene Figur zeigt nach unten", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40, Math.fround(Math.PI / 2)));
    drawFrame(ctx);
    expect(ctx.images().at(-1)?.sx).toBe(frameRect(rig, "idle", "down", now).sx);
  });

  it("AK-7: eigene Figur läuft", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    press("KeyD");
    now = 150;
    drawFrame(ctx);
    expect(frameRect(rig, "walk", "right", 150).sy).not.toBe(frameRect(rig, "idle", "right", 150).sy);
    expectCell(ctx.images().at(-1), frameRect(rig, "walk", "right", 150));
  });

  it("AK-8: eigene Figur bleibt stehen, Richtung bleibt", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    press("KeyD");
    now = 150;
    drawFrame(ctx);
    release("KeyD");
    now = 300;
    drawFrame(ctx);
    expectCell(ctx.images().at(-1), frameRect(rig, "idle", "right", 300));
  });

  it("AK-7: fremde Figur läuft zwischen zwei Snapshots", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    now = 100;
    socket.receive(snapshot(60));
    now = 100 + INTERPOLATION_DELAY_MS / 2;
    drawFrame(ctx);
    const images = ctx.images();
    expectCell(images[0], frameRect(rig, "walk", spriteDirection(Math.fround(-2)), now));
    expectCell(images.at(-1), frameRect(rig, "idle", spriteDirection(Math.fround(1.25)), now));
  });

  it("AK-8: fremde Figur ohne Positionsänderung steht", () => {
    const { ctx, game, socket } = setup();
    game.setSprites(spriteSet());
    socket.receive(welcome);
    socket.receive(snapshot(40));
    now = 100;
    socket.receive(snapshot(40));
    now = 100 + INTERPOLATION_DELAY_MS / 2;
    drawFrame(ctx);
    expectCell(ctx.images()[0], frameRect(rig, "idle", spriteDirection(Math.fround(-2)), now));
  });
});
