import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatRejected_Reason, Kicked_Reason } from "@/generated/browsergame/v1/game_pb";
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

function recordEvents() {
  const log: unknown[] = [];
  const events: GameEvents = {
    onStatus: (s) => { log.push(["status", s]); },
    onWelcome: (n) => { log.push(["welcome", n]); },
    onKicked: (r) => { log.push(["kicked", r]); },
    onCoordinates: () => {},
    onChatHistory: (m) => { log.push(["history", m.map((x) => x.text)]); },
    onChatMessage: (m) => { log.push(["message", m.senderName, m.text, m.sentAtUnixMs]); },
    onChatRejected: (r) => { log.push(["rejected", r]); },
  };
  return { log, events };
}

function startGame() {
  const { log, events } = recordEvents();
  const game = new Game(fakeCanvas(), events);
  game.start();
  return { game, log, socket: FakeWebSocket.latest() };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "Tobias", speed: 100, tickRateHz: 20 },
});

describe("Game: Chat", () => {
  it("reicht Verlauf, Nachricht und Ablehnung in Reihenfolge an GameEvents weiter", () => {
    const { log, socket } = startGame();
    socket.receive(welcome);
    socket.receive(serverBytes({
      case: "chatHistory",
      value: { messages: [{ senderName: "A", text: "alt", sentAtUnixMs: 1n }] },
    }));
    socket.receive(serverBytes({ case: "chatMessage", value: { senderName: "B", text: "neu", sentAtUnixMs: 2n } }));
    socket.receive(serverBytes({ case: "chatRejected", value: { reason: ChatRejected_Reason.TOO_FAST } }));
    expect(log).toEqual([
      ["status", "connecting"],
      ["welcome", "Tobias"],
      ["status", "connected"],
      ["history", ["alt"]],
      ["message", "B", "neu", 2n],
      ["rejected", ChatRejected_Reason.TOO_FAST],
    ]);
  });

  it("ohne Verlauf kommt nach Welcome kein Chat-Ereignis", () => {
    const { log, socket } = startGame();
    socket.receive(welcome);
    expect(log.some((e) => (e as unknown[])[0] === "welcome")).toBe(true);
    expect(log.some((e) => (e as unknown[])[0] === "history")).toBe(false);
  });

  it("Verbindungsende und Kicked lösen kein Chat-Ereignis aus", () => {
    const first = startGame();
    first.socket.receive(welcome);
    first.socket.receive(serverBytes({ case: "kicked", value: { reason: Kicked_Reason.SIGNED_IN_ELSEWHERE } }));
    first.socket.closeFromServer();
    expect(first.log.slice(-2)).toEqual([["kicked", Kicked_Reason.SIGNED_IN_ELSEWHERE], ["status", "kicked"]]);
    const chatKinds = ["history", "message", "rejected"];
    expect(first.log.some((e) => chatKinds.includes((e as string[])[0] ?? ""))).toBe(false);

    const second = startGame();
    second.socket.receive(welcome);
    second.socket.closeFromServer();
    expect(second.log.at(-1)).toEqual(["status", "disconnected"]);
    expect(second.log.some((e) => chatKinds.includes((e as string[])[0] ?? ""))).toBe(false);
  });

  it("sendChat schickt den Text und meldet Erfolg", () => {
    const { game, socket } = startGame();
    expect(game.sendChat(" hallo ")).toBe(true);
    expect(socket.sentChatTexts()).toEqual([" hallo "]);
  });

  it("sendChat ohne Verbindung meldet false und wirft nicht", () => {
    expect(new Game(fakeCanvas(), recordEvents().events).sendChat("x")).toBe(false);

    const stopped = startGame();
    stopped.game.stop();
    expect(stopped.game.sendChat("x")).toBe(false);
    expect(stopped.socket.sentCount()).toBe(0);

    const closed = startGame();
    closed.socket.closeFromServer();
    expect(closed.game.sendChat("x")).toBe(false);
    expect(closed.socket.sentCount()).toBe(0);
  });
});
