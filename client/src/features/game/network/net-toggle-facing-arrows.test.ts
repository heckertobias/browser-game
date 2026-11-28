import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { connect, createReceiver, type ChatHandlers, type ConnectionHandlers } from "./net";
import { FakeWebSocket, serverBytes } from "./fakeWebSocket";

function recorder() {
  const chat: string[] = [];
  let toggles = 0;
  const handlers = {
    onWelcome: () => {},
    onSnapshot: () => {},
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => { chat.push("history"); },
    onChatMessage: () => { chat.push("message"); },
    onChatRejected: () => { chat.push("rejected"); },
    onCommandRejected: () => { chat.push("commandRejected"); },
    onToggleFacingArrows: () => { toggles++; },
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, chat, toggles: () => toggles };
}

const toggle = serverBytes({ case: "toggleFacingArrows", value: {} });

describe("createReceiver: ToggleFacingArrows (017 AK-11)", () => {
  it("eine Nachricht ruft den Handler genau einmal", () => {
    const r = recorder();
    createReceiver(r.handlers).message(toggle);
    expect(r.toggles()).toBe(1);
  });

  it("zwei Nachrichten rufen ihn zweimal", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(toggle);
    receiver.message(toggle);
    expect(r.toggles()).toBe(2);
  });

  it("ist keine Chat-Nachricht und keine Ablehnung", () => {
    const r = recorder();
    createReceiver(r.handlers).message(toggle);
    expect(r.chat).toEqual([]);
  });

  it("ohne Handler wirft nichts", () => {
    const { onToggleFacingArrows: _unused, ...handlers } = recorder().handlers;
    const receiver = createReceiver(handlers);
    expect(() => { receiver.message(toggle); }).not.toThrow();
  });
});

describe("connect: ToggleFacingArrows", () => {
  beforeEach(() => {
    FakeWebSocket.instances.length = 0;
    vi.stubGlobal("WebSocket", FakeWebSocket);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("reicht die Nachricht vom Socket weiter", () => {
    const r = recorder();
    connect("ws://test/ws", r.handlers);
    FakeWebSocket.latest().receive(toggle);
    expect(r.toggles()).toBe(1);
  });
});
