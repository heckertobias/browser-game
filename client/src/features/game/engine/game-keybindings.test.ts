import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { create } from "@bufbuild/protobuf";
import { Action, KeyBindingsSchema, type KeyBindings } from "@/generated/browsergame/v1/game_pb";
import { DEFAULT_BINDINGS, fromProto, toProto, type Bindings } from "@/features/game/input/bindings";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { Game } from "./game";

let frame: FrameRequestCallback | undefined;
let now = 0;

beforeEach(() => {
  FakeWebSocket.instances.length = 0;
  now = 0;
  frame = undefined;
  vi.stubGlobal("WebSocket", FakeWebSocket);
  vi.stubGlobal("ResizeObserver", class {
    observe() {}
    disconnect() {}
  });
  vi.stubGlobal("window", Object.assign(new EventTarget(), {
    location: { protocol: "http:", host: "test" },
    devicePixelRatio: 1,
  }));
  vi.stubGlobal("document", Object.assign(new EventTarget(), { hidden: false, visibilityState: "visible" }));
  vi.stubGlobal("performance", { now: () => 0 });
  vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback) => {
    frame = cb;
    return 1;
  });
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const fakeCanvas = () =>
  Object.assign(new EventTarget(), { getContext: () => ({}) }) as unknown as HTMLCanvasElement;

const custom = create(KeyBindingsSchema, {
  bindings: [
    { action: Action.MOVE_UP, codes: ["ArrowUp", "KeyW"] },
    { action: Action.MOVE_DOWN, codes: ["ArrowDown"] },
    { action: Action.MOVE_LEFT, codes: ["ArrowLeft"] },
    { action: Action.MOVE_RIGHT, codes: ["ArrowRight"] },
  ],
});

function createGame() {
  const log: string[] = [];
  const bindings: Bindings[] = [];
  const coordinates: string[] = [];
  const game = new Game(fakeCanvas(), {
    onStatus: () => {},
    onWelcome: () => { log.push("welcome"); },
    onKicked: () => {},
    onCoordinates: (text) => { coordinates.push(text); },
    onKeyBindings: (b) => { log.push("keyBindings"); bindings.push(b); },
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
  });
  return { game, log, bindings, coordinates };
}

function setup() {
  const created = createGame();
  created.game.start();
  return { ...created, socket: FakeWebSocket.latest() };
}

const welcomeWith = (keyBindings?: KeyBindings) =>
  serverBytes({ case: "welcome", value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, keyBindings } });

const snapshotAt = (x: number) =>
  serverBytes({
    case: "snapshot",
    value: { tick: 1, lastProcessedSeq: 0, players: [{ id: "a", name: "n", x, y: 0 }] },
  });

const press = (code: string) => window.dispatchEvent(Object.assign(new Event("keydown"), { code }));
const release = (code: string) => window.dispatchEvent(Object.assign(new Event("keyup"), { code }));
const step = () => {
  now += 50;
  frame?.(now);
};
const lastInput = (socket: FakeWebSocket) => socket.sentInputs().at(-1);

describe("Game: Belegung aus Welcome (AK-9)", () => {
  it("übernimmt die gespeicherte Belegung und meldet sie", () => {
    const { bindings, socket } = setup();
    socket.receive(welcomeWith(custom));
    socket.receive(snapshotAt(0));
    expect(bindings.at(-1)).toEqual(fromProto(custom));
    press("ArrowUp");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 0, dirY: -1 });
    release("ArrowUp");
    press("KeyD");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 0, dirY: 0 });
  });

  it("ohne gespeicherte Belegung gilt der Standard", () => {
    const { bindings, socket } = setup();
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    expect(bindings).toHaveLength(1);
    expect(bindings[0]).toBe(DEFAULT_BINDINGS);
    press("KeyD");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 1, dirY: 0 });
  });

  it("ein weiteres Welcome ersetzt die Belegung", () => {
    const { bindings, socket } = setup();
    socket.receive(welcomeWith(custom));
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    expect(bindings.at(-1)).toBe(DEFAULT_BINDINGS);
    press("ArrowUp");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 0, dirY: 0 });
  });

  it("meldet die Belegung vor dem Namen", () => {
    const { log, socket } = setup();
    socket.receive(welcomeWith(custom));
    expect(log).toEqual(["keyBindings", "welcome"]);
  });
});

describe("Game.saveKeyBindings (AK-5, AK-9)", () => {
  it("wirkt sofort, meldet und schickt die Belegung", () => {
    const { game, bindings, socket } = setup();
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    const next = fromProto(custom);
    game.saveKeyBindings(next);
    expect(bindings.at(-1)).toBe(next);
    expect(socket.sentKeyBindings()).toEqual([toProto(next)]);
    press("ArrowRight");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 1, dirY: 0 });
  });

  it("Standard wird als leere Belegung geschickt", () => {
    const { game, socket } = setup();
    socket.receive(welcomeWith(custom));
    game.saveKeyBindings(DEFAULT_BINDINGS);
    expect(socket.sentKeyBindings()[0]?.bindings).toEqual([]);
  });

  it("ohne offene Verbindung wirkt die Belegung lokal und es wird nichts gesendet", () => {
    const next = fromProto(custom);

    const unstarted = createGame();
    expect(() => { unstarted.game.saveKeyBindings(next); }).not.toThrow();
    expect(unstarted.bindings.at(-1)).toBe(next);

    const started = setup();
    started.socket.closeFromServer();
    expect(() => { started.game.saveKeyBindings(next); }).not.toThrow();
    expect(started.socket.sentKeyBindings()).toEqual([]);
    expect(started.bindings.at(-1)).toBe(next);
  });
});

describe("Game.setMenuOpen (AK-4)", () => {
  it("offenes Menü hält die Figur an und schickt weiter Eingaben", () => {
    const { game, socket } = setup();
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    press("KeyD");
    step();
    expect(lastInput(socket)?.dirX).toBe(1);

    game.setMenuOpen(true);
    step();
    expect(lastInput(socket)).toEqual({ dirX: 0, dirY: 0 });
    const before = socket.sentInputs().length;
    press("KeyD");
    step();
    expect(lastInput(socket)).toEqual({ dirX: 0, dirY: 0 });
    expect(socket.sentInputs().length).toBeGreaterThan(before);
  });

  it("nach dem Schließen steuert die nächste Taste wieder", () => {
    const { game, socket } = setup();
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    game.setMenuOpen(true);
    press("KeyD");
    step();
    game.setMenuOpen(false);
    release("KeyD");
    press("KeyD");
    step();
    expect(lastInput(socket)?.dirX).toBe(1);
  });

  it("Snapshots laufen bei offenem Menü weiter", () => {
    const { game, coordinates, socket } = setup();
    socket.receive(welcomeWith());
    socket.receive(snapshotAt(0));
    step();
    game.setMenuOpen(true);
    const before = coordinates.length;
    socket.receive(snapshotAt(100));
    step();
    expect(coordinates.length).toBeGreaterThan(before);
  });
});
