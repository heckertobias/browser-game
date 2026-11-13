import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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

const snapshot = (ownFacing: number) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      players: [
        { id: "a", name: "n", x: 0, y: 0, facing: ownFacing },
        { id: "b", name: "o", x: 40, y: 20, facing: -2 },
      ],
    },
  });

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

const press = (code: string) => window.dispatchEvent(Object.assign(new Event("keydown"), { code }));

function expectPoints(actual: readonly Vec[] | undefined, expected: readonly Vec[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((point, i) => {
    expect(actual?.[i]?.x).toBeCloseTo(point.x, 9);
    expect(actual?.[i]?.y).toBeCloseTo(point.y, 9);
  });
}

describe("Game: Pfeile im Bild (016)", () => {
  it("AK-6, AK-8: eigener Pfeil aus der Vorhersage, fremder aus dem Snapshot", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(1.25));
    drawFrame(ctx);
    const fills = ctx.paths("fill");
    expect(fills).toHaveLength(2);
    expectPoints(fills[0]?.points, arrowPoints(40, 20, -2));
    expectPoints(fills[1]?.points, arrowPoints(0, 0, 1.25));
  });

  it("AK-7: eigener Pfeil dreht mit dem nächsten Abgleich mit", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(1.25));
    drawFrame(ctx);
    socket.receive(snapshot(0.5));
    drawFrame(ctx);
    expectPoints(ctx.paths("fill").at(-1)?.points, arrowPoints(0, 0, 0.5));
  });

  it("AK-7: eigener Pfeil folgt der Vorhersage, nicht dem letzten Snapshot", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(1.25));
    press("KeyD");
    now += 50;
    drawFrame(ctx);
    const [tip, a, b] = ctx.paths("fill").at(-1)?.points ?? [];
    if (!tip || !a || !b) throw new Error("Pfeil fehlt");
    const angle = Math.atan2(tip.y - (a.y + b.y) / 2, tip.x - (a.x + b.x) / 2);
    expect(angle).toBeCloseTo(0, 6);
  });
});
