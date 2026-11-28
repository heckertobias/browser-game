import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { START_MARKER_COLOR } from "@/features/game/core/constants";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { FakeContext, type ImageOp } from "@/features/game/render/fakeContext";
import { parseAtlas, type Atlas } from "@/features/game/sprites/atlas";
import { GROUND_ENTRIES } from "@/features/game/world/groundTiles";
import { t } from "@/features/localization/i18n";
import { GroundKind } from "@/generated/browsergame/v1/game_pb";
import { Game } from "./game";

let frame: FrameRequestCallback | undefined;
const now = 0;

beforeEach(() => {
  frame = undefined;
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

/** Atlas mit eindeutigem Rechteck je Name, 8 Spalten zu 16 Pixeln. */
function testAtlas(names: readonly string[]): Atlas {
  return parseAtlas({
    image: "test.png",
    entries: Object.fromEntries(
      names.map((name, i) => [name, { x: (i % 8) * 16, y: Math.floor(i / 8) * 16, width: 16, height: 16 }]),
    ),
  });
}

const NAMES = [...GROUND_ENTRIES, "grass.1", "grass.2"];
const image = { id: "ground" } as unknown as CanvasImageSource;
const ground = (names: readonly string[] = NAMES) => ({ atlas: testAtlas(names), image });

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

interface Area {
  tileSize: number;
  width: number;
  height: number;
  originTx: number;
  originTy: number;
  ground: Uint8Array;
}

const welcomeWith = (area?: Area) =>
  serverBytes({
    case: "welcome",
    value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01, area },
  });

/** Weltkacheln x ≤ −1 Berg, x ≥ 0 Gras (Rand fortgesetzt). */
const AREA: Area = {
  tileSize: 16,
  width: 2,
  height: 1,
  originTx: -1,
  originTy: 0,
  ground: new Uint8Array([GroundKind.MOUNTAIN, GroundKind.GRASS]),
};

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

const groundImages = (ctx: FakeContext) => ctx.images().filter((op) => op.source === image);
const gridLines = (ctx: FakeContext) => ctx.rects().filter((r) => r.width <= 1 || r.height <= 1);
const markerRects = (ctx: FakeContext) => ctx.rects().filter((r) => r.style === START_MARKER_COLOR);

function nameOf(op: ImageOp, atlas: Atlas): string | undefined {
  return [...atlas.entries].find(([, e]) => e.x === op.sx && e.y === op.sy)?.[0];
}

function expectGrid(ctx: FakeContext): void {
  expect(groundImages(ctx)).toHaveLength(0);
  expect(gridLines(ctx).length).toBeGreaterThan(0);
}

describe("Game: Boden (018)", () => {
  it("AK-1: Karte aus dem Welcome und Atlas landen im Bild", () => {
    const { ctx, game, socket } = setup();
    const { atlas, image: img } = ground();
    game.setGroundAtlas({ atlas, image: img });
    socket.receive(welcomeWith(AREA));
    drawFrame(ctx);
    const ops = groundImages(ctx);
    expect(ops.length).toBeGreaterThan(0);
    expect(gridLines(ctx)).toHaveLength(0);
    for (const op of ops) {
      const name = nameOf(op, atlas);
      if (op.dx < 0) expect(name).toBe("mountain");
      else expect(name === "grass" || name?.startsWith("grass.") === true).toBe(true);
    }
  });

  it("Reihenfolge egal: Atlas nach dem Welcome", () => {
    const { ctx, game, socket } = setup();
    socket.receive(welcomeWith(AREA));
    drawFrame(ctx);
    expectGrid(ctx);
    game.setGroundAtlas(ground());
    drawFrame(ctx);
    expect(groundImages(ctx).length).toBeGreaterThan(0);
    expect(gridLines(ctx)).toHaveLength(0);
  });

  it("Entscheidung 9: ohne Karte im Welcome Gitter", () => {
    const { ctx, game, socket } = setup();
    game.setGroundAtlas(ground());
    socket.receive(welcomeWith());
    drawFrame(ctx);
    expectGrid(ctx);
  });

  it("Entscheidung 9: ohne Atlas Gitter", () => {
    const { ctx, socket } = setup();
    socket.receive(welcomeWith(AREA));
    drawFrame(ctx);
    expectGrid(ctx);
  });

  it("Entscheidung 9: setGroundAtlas(null) fällt auf das Gitter zurück", () => {
    const { ctx, game, socket } = setup();
    game.setGroundAtlas(ground());
    game.setGroundAtlas(null);
    socket.receive(welcomeWith(AREA));
    drawFrame(ctx);
    expectGrid(ctx);
  });

  it("Atlas ohne alle Namen aus GROUND_ENTRIES → Gitter", () => {
    const { ctx, game, socket } = setup();
    game.setGroundAtlas(ground(NAMES.filter((n) => n !== "mountain")));
    socket.receive(welcomeWith(AREA));
    drawFrame(ctx);
    expectGrid(ctx);
  });

  it.each([
    ["ohne Boden", false],
    ["mit Boden", true],
  ])("AK-7: Startmarkierung mit dem Text aus den Wörterbüchern, %s", (_, withGround) => {
    const { ctx, game, socket } = setup();
    if (withGround) game.setGroundAtlas(ground());
    socket.receive(welcomeWith(withGround ? AREA : undefined));
    drawFrame(ctx);
    expect(markerRects(ctx)).toHaveLength(2);
    expect(ctx.texts().some((op) => op.text === t.startMarker)).toBe(true);
  });
});
