import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CommandRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { connect, createReceiver, type ChatHandlers, type CommandRejectReason, type ConnectionHandlers } from "./net";
import { FakeWebSocket, serverBytes } from "./fakeWebSocket";

function recorder() {
  const chat: string[] = [];
  const rejections: CommandRejectReason[] = [];
  const handlers = {
    onWelcome: () => {},
    onSnapshot: () => {},
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => {},
    onChatMessage: () => { chat.push("message"); },
    onChatRejected: () => { chat.push("rejected"); },
    onCommandRejected: (r) => { rejections.push(r); },
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, chat, rejections };
}

const rejected = (reason: CommandRejected_Reason) => serverBytes({ case: "commandRejected", value: { reason } });

describe("createReceiver: CommandRejected (015 AK-4)", () => {
  it("unbekannter Befehl wird gemeldet", () => {
    const r = recorder();
    createReceiver(r.handlers).message(rejected(CommandRejected_Reason.UNKNOWN_COMMAND));
    expect(r.rejections).toEqual([CommandRejected_Reason.UNKNOWN_COMMAND]);
  });

  it("fehlerhafter Befehl wird gemeldet", () => {
    const r = recorder();
    createReceiver(r.handlers).message(rejected(CommandRejected_Reason.INVALID));
    expect(r.rejections).toEqual([CommandRejected_Reason.INVALID]);
  });

  it("ist keine Chat-Ablehnung und keine Nachricht", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(rejected(CommandRejected_Reason.UNKNOWN_COMMAND));
    receiver.message(rejected(CommandRejected_Reason.INVALID));
    expect(r.chat).toEqual([]);
  });

  it("Gründe ohne Bedeutung werden ignoriert", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(rejected(CommandRejected_Reason.UNSPECIFIED));
    const unknownReason: never = 99 as never;
    receiver.message(serverBytes({ case: "commandRejected", value: { reason: unknownReason } }));
    expect(r.rejections).toEqual([]);
  });

  it("ohne Handler wirft nichts", () => {
    const { onCommandRejected: _unused, ...handlers } = recorder().handlers;
    const receiver = createReceiver(handlers);
    expect(() => { receiver.message(rejected(CommandRejected_Reason.INVALID)); }).not.toThrow();
  });
});

describe("connect: CommandRejected", () => {
  beforeEach(() => {
    FakeWebSocket.instances.length = 0;
    vi.stubGlobal("WebSocket", FakeWebSocket);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("reicht die Ablehnung vom Socket weiter", () => {
    const r = recorder();
    connect("ws://test/ws", r.handlers);
    FakeWebSocket.latest().receive(rejected(CommandRejected_Reason.UNKNOWN_COMMAND));
    expect(r.rejections).toEqual([CommandRejected_Reason.UNKNOWN_COMMAND]);
  });
});
