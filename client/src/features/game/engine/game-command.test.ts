import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CommandRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { Game, type GameEvents } from "./game";

beforeEach(() => {
  FakeWebSocket.instances.length = 0;
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
  vi.stubGlobal("requestAnimationFrame", () => 1);
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const fakeCanvas = () =>
  Object.assign(new EventTarget(), { getContext: () => ({}) }) as unknown as HTMLCanvasElement;

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "Tobias", speed: 100, tickRateHz: 20 },
});
const rejected = (reason: CommandRejected_Reason) => serverBytes({ case: "commandRejected", value: { reason } });

function startGame(onCommandRejected?: GameEvents["onCommandRejected"]) {
  const statuses: string[] = [];
  const events: GameEvents = {
    onStatus: (s) => { statuses.push(s); },
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    ...(onCommandRejected ? { onCommandRejected } : {}),
  };
  const game = new Game(fakeCanvas(), events);
  game.start();
  return { statuses, socket: FakeWebSocket.latest() };
}

describe("Game: CommandRejected (015 AK-4)", () => {
  it("reicht CommandRejected an GameEvents weiter", () => {
    const log: CommandRejected_Reason[] = [];
    const { socket } = startGame((r) => { log.push(r); });
    socket.receive(welcome);
    socket.receive(rejected(CommandRejected_Reason.INVALID));
    socket.receive(rejected(CommandRejected_Reason.UNKNOWN_COMMAND));
    expect(log).toEqual([CommandRejected_Reason.INVALID, CommandRejected_Reason.UNKNOWN_COMMAND]);
  });

  it("ohne onCommandRejected wirft nichts", () => {
    const { statuses, socket } = startGame();
    socket.receive(welcome);
    expect(() => { socket.receive(rejected(CommandRejected_Reason.INVALID)); }).not.toThrow();
    expect(statuses.at(-1)).toBe("connected");
  });
});
