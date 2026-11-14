import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { create } from "@bufbuild/protobuf";
import {
  Attack_Kind,
  AttackRejected_Reason,
  TargetState_Kind,
  ProjectileStateSchema,
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

function setup(withTargetHandler = true, withRejectHandler = true) {
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
  const game = new Game(canvas, {
    onStatus: () => {},
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    ...(withRejectHandler ? { onAttackRejected: (reason: AttackRejectReason) => { rejections.push(reason); } } : {}),
    ...(withTargetHandler ? { onTarget: (id: string | null) => { targets.push(id); } } : {}),
  });
  game.start();
  return { ctx, socket: FakeWebSocket.latest(), game, targets, rejections, canvas, setPointerCapture };
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

describe("Game: AttackRejected (013)", () => {
  it("AK-8: zu wenig Mana erreicht onAttackRejected", () => {
    const { socket, rejections } = setup();
    socket.receive(welcome);
    socket.receive(rejected(AttackRejected_Reason.NOT_ENOUGH_MANA));
    expect(rejections).toEqual([AttackRejected_Reason.NOT_ENOUGH_MANA]);
  });

  it("AK-19: nicht vor dir erreicht onAttackRejected", () => {
    const { socket, rejections } = setup();
    socket.receive(welcome);
    socket.receive(rejected(AttackRejected_Reason.NOT_FACING));
    socket.receive(rejected(AttackRejected_Reason.NOT_ENOUGH_MANA));
    expect(rejections).toEqual([AttackRejected_Reason.NOT_FACING, AttackRejected_Reason.NOT_ENOUGH_MANA]);
  });

  it("ohne Handler wirft nichts", () => {
    const { socket } = setup(true, false);
    socket.receive(welcome);
    expect(() => { socket.receive(rejected(AttackRejected_Reason.NOT_FACING)); }).not.toThrow();
  });
});

describe("Game: Standardtasten (013 plan.md Entscheidung 8)", () => {
  it("1 löst ohne eigene Belegung den Nahkampf aus", () => {
    const { socket } = selectDummy();
    press("Digit1");
    const sent = socket.sentAttacks();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.kind).toBe(Attack_Kind.MELEE);
    expect(sent[0]?.targetId).toBe("dummy");
  });

  it("2 löst den Fernkampf aus", () => {
    const { socket } = selectDummy();
    press("Digit2");
    const sent = socket.sentAttacks();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.kind).toBe(Attack_Kind.RANGED);
    expect(sent[0]?.targetId).toBe("dummy");
  });

  it("AK-2: ohne Ziel schickt 1 nichts", () => {
    const { socket, ctx } = setup();
    socket.receive(welcome);
    socket.receive(snap({ targets: [dummy] }));
    drawFrame(ctx);
    press("Digit1");
    expect(socket.sentAttacks()).toEqual([]);
  });
});
