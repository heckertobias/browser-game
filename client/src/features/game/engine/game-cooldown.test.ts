import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { create } from "@bufbuild/protobuf";
import {
  AttackRejected_Reason,
  TargetState_Kind,
  TargetStateSchema,
} from "@/generated/browsergame/v1/game_pb";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { FakeContext } from "@/features/game/render/fakeContext";
import type { AttackRejectReason } from "@/features/game/network/net";
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

function setup(withCooldownHandler = true) {
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
  const rejections: AttackRejectReason[] = [];
  const cooldowns: number[] = [];
  const game = new Game(canvas, {
    onStatus: () => {},
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    onAttackRejected: (reason: AttackRejectReason) => { rejections.push(reason); },
    onTarget: (id: string | null) => { targets.push(id); },
    ...(withCooldownHandler ? { onCooldown: (ms: number) => { cooldowns.push(ms); } } : {}),
  });
  game.start();
  return { ctx, socket: FakeWebSocket.latest(), game, targets, rejections, cooldowns, canvas, setPointerCapture };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, minFacingInput: 0.01 },
});

type TargetInit = NonNullable<Parameters<typeof create<typeof TargetStateSchema>>[1]>;

const dummy: TargetInit = { id: "dummy", kind: TargetState_Kind.TRAINING_DUMMY, x: 0, y: -128, hp: 100, maxHp: 100 };

const snap = (init: { targets?: TargetInit[]; ownX?: number; ownY?: number } = {}) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      players: [{ id: "a", name: "n", x: init.ownX ?? 0, y: init.ownY ?? 0 }],
      targets: init.targets ?? [],
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

const rejected = (reason: AttackRejected_Reason) => serverBytes({ case: "attackRejected", value: { reason } });
const accepted = (cooldownSeconds: number) => serverBytes({ case: "attackAccepted", value: { cooldownSeconds } });

describe("Game: Cooldown-Sweep (013 AK-22)", () => {
  it("Bestätigung meldet die Abklingzeit in ms", () => {
    const { socket, cooldowns } = setup();
    socket.receive(welcome);
    socket.receive(accepted(1));
    expect(cooldowns).toEqual([1000]);
    socket.receive(accepted(0.5));
    expect(cooldowns).toEqual([1000, 500]);
  });

  it("Senden allein startet keinen Sweep", () => {
    const { socket, cooldowns } = selectDummy();
    press("Digit1");
    press("Digit2");
    expect(socket.sentAttacks()).toHaveLength(2);
    expect(cooldowns).toEqual([]);
  });

  it("AK-12: Ablehnung startet keinen Sweep", () => {
    const { socket, rejections, cooldowns } = setup();
    socket.receive(welcome);
    socket.receive(rejected(AttackRejected_Reason.OUT_OF_RANGE));
    socket.receive(rejected(AttackRejected_Reason.NOT_FACING));
    socket.receive(rejected(AttackRejected_Reason.NOT_ENOUGH_MANA));
    expect(rejections).toHaveLength(3);
    expect(cooldowns).toEqual([]);
  });

  it("der eigene Projektilstart startet keinen Sweep", () => {
    const { socket, cooldowns } = setup();
    socket.receive(welcome);
    socket.receive(
      serverBytes({ case: "combatEvents", value: { launched: [{ projectileId: 1, shooterId: "a", targetId: "dummy" }], hits: [] } }),
    );
    expect(cooldowns).toEqual([]);
  });

  it("ohne Handler wirft nichts", () => {
    const { socket } = setup(false);
    socket.receive(welcome);
    expect(() => { socket.receive(accepted(1)); }).not.toThrow();
  });
});
