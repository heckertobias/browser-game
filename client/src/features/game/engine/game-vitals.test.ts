import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeWebSocket, serverBytes } from "@/features/game/network/fakeWebSocket";
import { Game, type PlayerVitals } from "./game";

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
  vi.stubGlobal("performance", { now: () => 0 });
  vi.stubGlobal("requestAnimationFrame", () => 1);
  vi.stubGlobal("cancelAnimationFrame", () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const fakeCanvas = () =>
  Object.assign(new EventTarget(), { getContext: () => ({}) }) as unknown as HTMLCanvasElement;

function setup(withVitals = true) {
  const vitals: PlayerVitals[] = [];
  const game = new Game(fakeCanvas(), {
    onStatus: () => {},
    onWelcome: () => {},
    onKicked: () => {},
    onCoordinates: () => {},
    ...(withVitals ? { onVitals: (v: PlayerVitals) => { vitals.push(v); } } : {}),
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
  });
  game.start();
  return { vitals, socket: FakeWebSocket.latest() };
}

const welcome = serverBytes({
  case: "welcome",
  value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, maxHp: 100, maxMana: 100 },
});

const snapshot = (hp: number, mana: number, withSelf = true) =>
  serverBytes({
    case: "snapshot",
    value: {
      tick: 1,
      lastProcessedSeq: 0,
      hp,
      mana,
      players: [withSelf ? { id: "a", name: "n", x: 0, y: 0 } : { id: "b", name: "o", x: 0, y: 0 }],
    },
  });

describe("Game: HP und Mana (012 AK-5, AK-6)", () => {
  it("meldet nach Welcome und erstem Snapshot die Werte mit Maximum", () => {
    const { vitals, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(100, 100));
    expect(vitals).toEqual([{ hp: 100, maxHp: 100, mana: 100, maxMana: 100 }]);
  });

  it("meldet nur bei Änderung der abgerundeten Werte", () => {
    const { vitals, socket } = setup();
    socket.receive(welcome);
    for (const mana of [40, 40, 40.25, 40.75, 41]) socket.receive(snapshot(100, mana));
    expect(vitals.map((v) => v.mana)).toEqual([40, 41]);
  });

  it("meldet Schaden sofort mit dem nächsten Snapshot", () => {
    const { vitals, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(100, 100));
    socket.receive(snapshot(70, 100));
    expect(vitals).toHaveLength(2);
    expect(vitals.at(-1)).toEqual({ hp: 70, maxHp: 100, mana: 100, maxMana: 100 });
  });

  it("rundet ab", () => {
    const { vitals, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(73.9, 99.75));
    expect(vitals[0]).toMatchObject({ hp: 73, mana: 99 });
  });

  it("meldet ohne eigene Figur im Snapshot nichts", () => {
    const { vitals, socket } = setup();
    socket.receive(welcome);
    socket.receive(snapshot(0, 0, false));
    expect(vitals).toHaveLength(0);
    socket.receive(snapshot(100, 100));
    expect(vitals).toHaveLength(1);
    expect(vitals[0]?.hp).toBe(100);
  });

  it("meldet vor dem Welcome nichts", () => {
    const { vitals, socket } = setup();
    socket.receive(snapshot(100, 100));
    expect(vitals).toHaveLength(0);
  });

  it("wirft ohne onVitals-Handler nicht", () => {
    const { socket } = setup(false);
    expect(() => {
      socket.receive(welcome);
      socket.receive(snapshot(50, 50));
    }).not.toThrow();
  });
});
