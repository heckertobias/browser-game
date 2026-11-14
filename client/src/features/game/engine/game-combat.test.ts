import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { create } from "@bufbuild/protobuf";
import {
  Action,
  Attack_Kind,
  TargetState_Kind,
  ProjectileStateSchema,
  TargetStateSchema,
} from "@/generated/browsergame/v1/game_pb";
import { DUMMY_POST_WIDTH, HP_BAR_HEIGHT, HP_BAR_WIDTH, TARGET_HIT_RADIUS } from "@/features/game/core/constants";
import { DEFAULT_BINDINGS } from "@/features/game/input/bindings";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { FakeContext } from "@/features/game/render/fakeContext";
import { hpBarWidth } from "@/features/game/render/render";
import { Game } from "./game";

let frame: FrameRequestCallback | undefined;
let now = 0;
let clock = 0;

beforeEach(() => {
  frame = undefined;
  now = 0;
  clock = 0;
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
  vi.stubGlobal("performance", { now: () => clock });
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    frame = callback;
    return 1;
  });
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function setup(withTargetHandler = true) {
  const ctx = new FakeContext();
  const setPointerCapture = vi.fn();
  const canvas = Object.assign(new EventTarget(), {
    getContext: () => ctx.asContext(),
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 640, height: 360 }),
    setPointerCapture,
    width: 0,
    height: 0,
  }) as unknown as HTMLCanvasElement;
  const targets: (string | null)[] = [];
  const game = new Game(canvas, {
    onStatus: () => {},
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    ...(withTargetHandler ? { onTarget: (id: string | null) => { targets.push(id); } } : {}),
  });
  game.start();
  return { ctx, socket: FakeWebSocket.latest(), game, targets, canvas, setPointerCapture };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01 },
});

type TargetInit = NonNullable<Parameters<typeof create<typeof TargetStateSchema>>[1]>;
type ProjectileInit = NonNullable<Parameters<typeof create<typeof ProjectileStateSchema>>[1]>;

const dummy: TargetInit = { id: "dummy", kind: TargetState_Kind.TRAINING_DUMMY, x: 0, y: -128, hp: 100, maxHp: 100 };

const snap = (init: { targets?: TargetInit[]; projectiles?: ProjectileInit[]; ownX?: number; ownY?: number } = {}) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      players: [{ id: "a", name: "n", x: init.ownX ?? 0, y: init.ownY ?? 0 }],
      targets: init.targets ?? [],
      projectiles: init.projectiles ?? [],
    },
  });

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

function click(canvas: HTMLCanvasElement, clientX: number, clientY: number): void {
  canvas.dispatchEvent(
    Object.assign(new Event("pointerdown", { cancelable: true }), { pointerId: 1, isPrimary: true, button: 0, clientX, clientY }),
  );
  canvas.dispatchEvent(Object.assign(new Event("pointerup"), { pointerId: 1 }));
}

const press = (code: string) => window.dispatchEvent(Object.assign(new Event("keydown"), { code }));
const posts = (ctx: FakeContext) => ctx.rects().filter((r) => r.width === DUMMY_POST_WIDTH);
const outlines = (ctx: FakeContext) => ctx.paths("stroke").filter((p) => p.points.length === 4);
const fillWidth = (ctx: FakeContext) => ctx.rects().filter((r) => r.height === HP_BAR_HEIGHT)[1]?.width;

/** Weltpunkt (0, -128) liegt bei Client (320, 52). */
const DUMMY_CLIENT = { x: 320, y: 52 };

function selectDummy() {
  const env = setup();
  env.socket.receive(welcome);
  env.socket.receive(snap({ targets: [dummy] }));
  drawFrame(env.ctx);
  click(env.canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y);
  return env;
}

describe("Game: Ziele und Auswahl (013)", () => {
  it("AK-1: Klick auf den Dummy wählt ihn aus und umrandet ihn", () => {
    const { ctx, targets, setPointerCapture } = selectDummy();
    expect(targets).toEqual(["dummy"]);
    expect(setPointerCapture).not.toHaveBeenCalled();
    drawFrame(ctx);
    expect(outlines(ctx)).toHaveLength(1);
  });

  it("AK-1: Klick trifft den Dummy auch bei Kamera abseits des Ursprungs", () => {
    const { ctx, socket, canvas, targets } = setup();
    socket.receive(welcome);
    socket.receive(snap({ ownX: 100, ownY: 50, targets: [{ ...dummy, x: 100, y: -78 }] }));
    drawFrame(ctx);
    click(canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y);
    expect(targets).toEqual(["dummy"]);
  });

  it("AK-1: Klick daneben wählt nichts aus und läuft los", () => {
    const { ctx, socket, canvas, targets, setPointerCapture } = setup();
    socket.receive(welcome);
    socket.receive(snap({ targets: [dummy] }));
    drawFrame(ctx);
    click(canvas, DUMMY_CLIENT.x + TARGET_HIT_RADIUS + 5, DUMMY_CLIENT.y);
    expect(targets).toEqual([]);
    expect(setPointerCapture).toHaveBeenCalled();
    drawFrame(ctx);
    expect(outlines(ctx)).toHaveLength(0);
  });

  it("erneuter Klick auf dasselbe Ziel meldet nichts Neues", () => {
    const { canvas, targets } = selectDummy();
    click(canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y);
    expect(targets).toEqual(["dummy"]);
  });

  it("plan.md Entscheidung 2: fehlt das Ziel im Snapshot, wird die Auswahl aufgehoben", () => {
    const { ctx, socket, targets } = selectDummy();
    socket.receive(snap({ targets: [] }));
    expect(targets).toEqual(["dummy", null]);
    drawFrame(ctx);
    expect(outlines(ctx)).toHaveLength(0);
    expect(posts(ctx)).toHaveLength(0);
    socket.receive(snap({ targets: [dummy] }));
    drawFrame(ctx);
    expect(posts(ctx)).toHaveLength(1);
    expect(outlines(ctx)).toHaveLength(0);
    expect(targets).toEqual(["dummy", null]);
  });

  it("AK-18: HP-Balken folgt dem Snapshot sofort", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    socket.receive(snap({ targets: [dummy] }));
    drawFrame(ctx);
    expect(fillWidth(ctx)).toBe(hpBarWidth(100, 100, HP_BAR_WIDTH));
    socket.receive(snap({ targets: [{ ...dummy, hp: 85 }] }));
    drawFrame(ctx);
    expect(fillWidth(ctx)).toBe(hpBarWidth(85, 100, HP_BAR_WIDTH));
  });

  it("Ziele werden nicht interpoliert", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    clock = 0;
    socket.receive(snap({ targets: [dummy] }));
    clock = 50;
    socket.receive(snap({ targets: [{ ...dummy, x: 10 }] }));
    now = 75;
    drawFrame(ctx);
    expect(posts(ctx)[0]?.x).toBe(10 - DUMMY_POST_WIDTH / 2);
  });

  it("ohne onTarget-Handler wirft die Auswahl nicht", () => {
    const { ctx, socket, canvas } = setup(false);
    socket.receive(welcome);
    socket.receive(snap({ targets: [dummy] }));
    drawFrame(ctx);
    expect(() => { click(canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y); }).not.toThrow();
  });
});

describe("Game: Angriff (013)", () => {
  it("AK-2: ohne Ziel schickt attack nichts", () => {
    const { socket, game } = setup();
    socket.receive(welcome);
    socket.receive(snap({ targets: [dummy] }));
    game.attack(Attack_Kind.MELEE);
    expect(socket.sentAttacks()).toEqual([]);
  });

  it("AK-7: Fernkampf auf das gewählte Ziel wird geschickt", () => {
    const { socket, game } = selectDummy();
    game.attack(Attack_Kind.RANGED);
    const sent = socket.sentAttacks();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.kind).toBe(Attack_Kind.RANGED);
    expect(sent[0]?.targetId).toBe("dummy");
  });

  it("die Angriffstaste löst attack aus", () => {
    const { socket, game } = selectDummy();
    game.saveKeyBindings(new Map(DEFAULT_BINDINGS).set(Action.ATTACK_MELEE, ["Digit1"]));
    press("Digit1");
    const sent = socket.sentAttacks();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.kind).toBe(Attack_Kind.MELEE);
    expect(sent[0]?.targetId).toBe("dummy");
  });

  it("nach Aufheben der Auswahl kein Angriff mehr", () => {
    const { socket, game } = selectDummy();
    socket.receive(snap({ targets: [] }));
    game.attack(Attack_Kind.MELEE);
    expect(socket.sentAttacks()).toEqual([]);
  });
});

describe("Game: Projektile (013, AK-7)", () => {
  it("wird interpoliert gezeichnet", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    clock = 0;
    socket.receive(snap({ projectiles: [{ id: 1, x: 0, y: 0 }] }));
    clock = 50;
    socket.receive(snap({ projectiles: [{ id: 1, x: 20, y: 0 }] }));
    now = 125;
    drawFrame(ctx);
    const arcs = ctx.arcs();
    expect(arcs).toHaveLength(1);
    expect(arcs[0]?.x).toBeCloseTo(10, 9);
    expect(arcs[0]?.y).toBeCloseTo(0, 9);
  });

  it("nach dem Einschlag nicht mehr gezeichnet", () => {
    const { ctx, socket } = setup();
    socket.receive(welcome);
    clock = 0;
    socket.receive(snap({ projectiles: [{ id: 1, x: 0, y: 0 }] }));
    clock = 50;
    socket.receive(snap({ projectiles: [{ id: 1, x: 20, y: 0 }] }));
    clock = 100;
    socket.receive(snap({ projectiles: [] }));
    now = 300;
    drawFrame(ctx);
    expect(ctx.arcs()).toHaveLength(0);
  });
});
