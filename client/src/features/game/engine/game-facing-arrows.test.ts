import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PLAYER_SIZE } from "@/features/game/core/constants";
import type { Vec } from "@/features/game/core/vector";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { FakeContext } from "@/features/game/render/fakeContext";
import { arrowPoints } from "@/features/game/render/render";
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
  vi.stubGlobal("performance", { now: () => 0 });
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    frame = callback;
    return 1;
  });
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

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
  return { ctx, socket: FakeWebSocket.latest() };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01 },
});

const snapshot = serverBytes({
  case: "snapshot",
  value: {
    tick: 1,
    lastProcessedSeq: 0,
    players: [
      { id: "a", name: "n", x: 0, y: 0, facing: 1.25 },
      { id: "b", name: "o", x: 40, y: 20, facing: -2 },
    ],
  },
});

const toggle = serverBytes({ case: "toggleFacingArrows", value: {} });

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

const arrows = (ctx: FakeContext) => ctx.paths("fill");

function expectPoints(actual: readonly Vec[] | undefined, expected: readonly Vec[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((point, i) => {
    expect(actual?.[i]?.x).toBeCloseTo(point.x, 9);
    expect(actual?.[i]?.y).toBeCloseTo(point.y, 9);
  });
}

describe("Game: Pfeile als Entwickleroption (017)", () => {
  it("AK-10: ohne Schalter kein Pfeil", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot);
    drawFrame(ctx);
    expect(arrows(ctx)).toHaveLength(0);
    expect(ctx.rects().filter((r) => r.width === PLAYER_SIZE && r.height === PLAYER_SIZE)).toHaveLength(2);
  });

  it("AK-11: einmal umschalten zeigt den Pfeil an allen Figuren", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot);
    socket.receive(toggle);
    drawFrame(ctx);
    const fills = arrows(ctx);
    expect(fills).toHaveLength(2);
    expectPoints(fills[0]?.points, arrowPoints(40, 20, -2));
    expectPoints(fills[1]?.points, arrowPoints(0, 0, 1.25));
  });

  it("AK-11: zweites Mal blendet aus", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot);
    socket.receive(toggle);
    drawFrame(ctx);
    expect(arrows(ctx)).toHaveLength(2);
    socket.receive(toggle);
    drawFrame(ctx);
    expect(arrows(ctx)).toHaveLength(0);
  });

  it("AK-11: der Schalter wirkt auch auf später eintreffende Figuren", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(toggle);
    socket.receive(snapshot);
    drawFrame(ctx);
    expect(arrows(ctx)).toHaveLength(2);
  });

  it("AK-13: eine neue Instanz startet ohne Pfeile", () => {
    const first = setup();
    first.socket.receive(welcome);
    first.socket.receive(snapshot);
    first.socket.receive(toggle);
    drawFrame(first.ctx);
    expect(arrows(first.ctx)).toHaveLength(2);

    const second = setup();
    second.socket.receive(welcome);
    second.socket.receive(snapshot);
    drawFrame(second.ctx);
    expect(arrows(second.ctx)).toHaveLength(0);
  });
});
