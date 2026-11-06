import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { create } from "@bufbuild/protobuf";
import { Action, KeyBindingsSchema, type Welcome } from "@/generated/browsergame/v1/game_pb";
import { connect, createReceiver, type ChatHandlers, type ConnectionHandlers } from "./net";
import { FakeWebSocket, serverBytes } from "./fakeWebSocket";

function recorder() {
  const welcomes: Welcome[] = [];
  const handlers = {
    onWelcome: (w) => { welcomes.push(w); },
    onSnapshot: () => {},
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, welcomes };
}

const custom = create(KeyBindingsSchema, {
  bindings: [
    { action: Action.MOVE_UP, codes: ["ArrowUp", "KeyW"] },
    { action: Action.MOVE_DOWN, codes: ["ArrowDown"] },
    { action: Action.MOVE_LEFT, codes: ["ArrowLeft"] },
    { action: Action.MOVE_RIGHT, codes: ["ArrowRight"] },
  ],
});

beforeEach(() => {
  FakeWebSocket.instances.length = 0;
  vi.stubGlobal("WebSocket", FakeWebSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("createReceiver: Belegung (AK-9)", () => {
  it("reicht die Belegung aus Welcome unverändert an onWelcome weiter", () => {
    const r = recorder();
    createReceiver(r.handlers).message(
      serverBytes({ case: "welcome", value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20, keyBindings: custom } }),
    );
    expect(r.welcomes[0]?.keyBindings).toEqual(custom);
  });

  it("Welcome ohne Belegung liefert keine", () => {
    const r = recorder();
    createReceiver(r.handlers).message(
      serverBytes({ case: "welcome", value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20 } }),
    );
    expect(r.welcomes).toHaveLength(1);
    expect(r.welcomes[0]?.keyBindings?.bindings ?? []).toEqual([]);
  });
});

describe("connect: Belegung", () => {
  it("saveKeyBindings schickt die Belegung über den offenen Socket", () => {
    const connection = connect("ws://test/ws", recorder().handlers);
    expect(connection.saveKeyBindings(custom)).toBe(true);
    expect(FakeWebSocket.latest().sentKeyBindings()).toEqual([custom]);
  });

  it("saveKeyBindings mit leerer Belegung schickt eine leere Nachricht", () => {
    const connection = connect("ws://test/ws", recorder().handlers);
    expect(connection.saveKeyBindings(create(KeyBindingsSchema))).toBe(true);
    const sent = FakeWebSocket.latest().sentKeyBindings();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.bindings).toEqual([]);
  });

  it.each([FakeWebSocket.CONNECTING, FakeWebSocket.CLOSING, FakeWebSocket.CLOSED])(
    "saveKeyBindings sendet bei nicht offener Verbindung (Zustand %i) nichts und wirft nicht",
    (state) => {
      const connection = connect("ws://test/ws", recorder().handlers);
      FakeWebSocket.latest().readyState = state;
      expect(connection.saveKeyBindings(custom)).toBe(false);
      expect(FakeWebSocket.latest().sentCount()).toBe(0);
    },
  );
});
