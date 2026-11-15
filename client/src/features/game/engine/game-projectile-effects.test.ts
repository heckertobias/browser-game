import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { create } from "@bufbuild/protobuf";
import {
  TargetState_Kind,
  TargetStateSchema,
} from "@/generated/browsergame/v1/game_pb";
import { DUMMY_POST_WIDTH, HP_BAR_HEIGHT, HP_BAR_WIDTH, PROJECTILE_EFFECT_MAX_MS } from "@/features/game/core/constants";
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

const welcome = (projectileSpeed?: number) =>
  serverBytes({
    case: "welcome",
    value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01, ...(projectileSpeed ? { projectileSpeed } : {}) },
  });

type TargetInit = NonNullable<Parameters<typeof create<typeof TargetStateSchema>>[1]>;

const dummyAt = (hp: number): TargetInit => ({ id: "dummy", kind: TargetState_Kind.TRAINING_DUMMY, x: 0, y: -100, hp, maxHp: 100 });

const snap = (init: { targets?: TargetInit[]; others?: { id: string; x: number; y: number }[] } = {}) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      players: [{ id: "a", name: "n", x: 0, y: 0 }, ...(init.others ?? []).map((o) => ({ name: o.id, ...o }))],
      targets: init.targets ?? [],
    },
  });

const launched = (projectileId: number, shooterId = "a", targetId = "dummy") =>
  serverBytes({ case: "combatEvents", value: { launched: [{ projectileId, shooterId, targetId }], hits: [] } });
const hit = (projectileId: number, damage = 15) =>
  serverBytes({ case: "combatEvents", value: { launched: [], hits: [{ projectileId, targetId: "dummy", damage }] } });

function drawFrame(ctx: FakeContext): void {
  ctx.ops.length = 0;
  frame?.(now);
}

function drawAt(ctx: FakeContext, time: number): void {
  now = time;
  drawFrame(ctx);
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
const full = (hp: number) => hpBarWidth(hp, 100, HP_BAR_WIDTH);

/** Weltpunkt (0, -100) liegt bei Client (320, 80). */
const DUMMY_CLIENT = { x: 320, y: 80 };

function ready(speed = 400, others: { id: string; x: number; y: number }[] = []) {
  const env = setup();
  env.socket.receive(welcome(speed));
  env.socket.receive(snap({ targets: [dummyAt(100)], others }));
  return env;
}

describe("Game: Projektil-Effekt und zurückgehaltene HP (013 AK-21)", () => {
  it("eigener Fernkampf: Effekt startet an der vorhergesagten Position, HP sinken erst beim Einschlag", () => {
    const { ctx, socket } = ready();
    press("KeyD");
    drawAt(ctx, 125);
    clock = 125;
    socket.receive(launched(1));
    drawAt(ctx, 125);
    const arcs = ctx.arcs();
    expect(arcs).toHaveLength(1);
    // Server meldet (0, 0); die Vorhersage steht 7,5 weiter rechts.
    expect(arcs[0]?.x).toBeGreaterThan(1);
    expect(arcs[0]?.y).toBeCloseTo(0, 9);

    clock = 200;
    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, 200);
    expect(fillWidth(ctx)).toBe(full(100));

    drawAt(ctx, 450);
    expect(fillWidth(ctx)).toBe(full(85));
    expect(ctx.arcs()).toHaveLength(0);
  });

  it("Zuschauer: der Effekt startet an der interpolierten Position des Schützen", () => {
    const { ctx, socket } = ready(400, [{ id: "b", x: 40, y: 0 }]);
    clock = 50;
    socket.receive(snap({ targets: [dummyAt(100)], others: [{ id: "b", x: 80, y: 0 }] }));
    clock = 125;
    socket.receive(launched(1, "b"));
    drawAt(ctx, 125);
    expect(ctx.arcs()[0]?.x).toBeCloseTo(60, 9);
    expect(ctx.arcs()[0]?.y).toBeCloseTo(0, 9);

    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)], others: [{ id: "b", x: 80, y: 0 }] }));
    drawAt(ctx, 200);
    expect(fillWidth(ctx)).toBe(full(100));
    drawAt(ctx, 450);
    expect(fillWidth(ctx)).toBe(full(85));
  });

  it("ein Treffer ohne bekannten Start wirkt sofort", () => {
    const { ctx, socket } = ready();
    socket.receive(hit(9));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, 0);
    expect(fillWidth(ctx)).toBe(full(85));
  });

  it.each([["x", "dummy"], ["a", "y"]])("Schütze %s oder Ziel %s unbekannt: kein Effekt, HP sofort", (shooter, target) => {
    const { ctx, socket } = ready();
    socket.receive(launched(1, shooter, target));
    drawAt(ctx, 50);
    expect(ctx.arcs()).toHaveLength(0);
    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, 60);
    expect(fillWidth(ctx)).toBe(full(85));
  });

  it("ohne Tempo im Welcome: kein Effekt, HP sofort", () => {
    const { ctx, socket } = ready(0);
    socket.receive(launched(1));
    drawAt(ctx, 50);
    expect(ctx.arcs()).toHaveLength(0);
    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, 60);
    expect(fillWidth(ctx)).toBe(full(85));
  });

  it("maximale Wartezeit", () => {
    const { ctx, socket } = ready(1);
    socket.receive(launched(1));
    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, PROJECTILE_EFFECT_MAX_MS - 1);
    expect(fillWidth(ctx)).toBe(full(100));
    drawAt(ctx, PROJECTILE_EFFECT_MAX_MS);
    expect(fillWidth(ctx)).toBe(full(85));
    expect(ctx.arcs()).toHaveLength(0);
  });

  it("Nahkampf erscheint sofort, auch während ein Fernkampftreffer zurückgehalten wird", () => {
    const { ctx, socket } = ready();
    socket.receive(launched(1));
    socket.receive(hit(1));
    socket.receive(snap({ targets: [dummyAt(85)] }));
    drawAt(ctx, 50);
    expect(fillWidth(ctx)).toBe(full(100));
    socket.receive(snap({ targets: [dummyAt(75)] }));
    drawAt(ctx, 100);
    expect(fillWidth(ctx)).toBe(full(90));
  });

  it("tödlicher Treffer: der Dummy bleibt bis zum Einschlag sichtbar, die Auswahl folgt dem neuesten Snapshot", () => {
    const env = setup();
    env.socket.receive(welcome(400));
    env.socket.receive(snap({ targets: [dummyAt(15)] }));
    drawAt(env.ctx, 0);
    click(env.canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y);
    expect(env.targets).toEqual(["dummy"]);

    env.socket.receive(launched(1));
    env.socket.receive(hit(1, 15));
    env.socket.receive(snap({ targets: [] }));
    expect(env.targets).toEqual(["dummy", null]);

    drawAt(env.ctx, 100);
    expect(posts(env.ctx)).toHaveLength(1);
    expect(fillWidth(env.ctx)).toBe(full(15));
    expect(outlines(env.ctx)).toHaveLength(0);

    click(env.canvas, DUMMY_CLIENT.x, DUMMY_CLIENT.y);
    expect(env.targets).toEqual(["dummy", null]);

    drawAt(env.ctx, 250);
    expect(posts(env.ctx)).toHaveLength(0);
  });

  it("ein neues Welcome löscht die Effekte", () => {
    const { ctx, socket } = ready();
    socket.receive(launched(1));
    drawAt(ctx, 50);
    expect(ctx.arcs()).toHaveLength(1);
    socket.receive(welcome(400));
    drawAt(ctx, 60);
    expect(ctx.arcs()).toHaveLength(0);
  });
});
