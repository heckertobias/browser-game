import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatRejected_Reason, Kicked_Reason, type ChatMessage } from "@/generated/browsergame/v1/game_pb";
import {
  connect,
  createReceiver,
  type ChatHandlers,
  type ChatRejectReason,
  type ConnectionHandlers,
} from "./net";
import { FakeWebSocket, serverBytes } from "./fakeWebSocket";

function recorder() {
  const log: string[] = [];
  const histories: (readonly ChatMessage[])[] = [];
  const messages: ChatMessage[] = [];
  const rejections: ChatRejectReason[] = [];
  const handlers = {
    onWelcome: () => { log.push("welcome"); },
    onSnapshot: () => { log.push("snapshot"); },
    onKicked: () => { log.push("kicked"); },
    onClose: () => { log.push("close"); },
    onChatHistory: (m) => { log.push("history"); histories.push(m); },
    onChatMessage: (m) => { log.push("message"); messages.push(m); },
    onChatRejected: (r) => { log.push("rejected"); rejections.push(r); },
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, log, histories, messages, rejections };
}

const welcome = { case: "welcome", value: { playerId: "a", name: "n", speed: 100, tickRateHz: 20 } } as const;
const chat = (text: string) =>
  serverBytes({ case: "chatMessage", value: { senderName: "A", text, sentAtUnixMs: 1n } });

describe("createReceiver: Chat", () => {
  it("reicht den Verlauf in Server-Reihenfolge mit allen Feldern weiter", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(
      serverBytes({
        case: "chatHistory",
        value: {
          messages: [
            { senderName: "A", text: "eins", sentAtUnixMs: 1730000000000n },
            { senderName: "B", text: "zwei", sentAtUnixMs: 1730000001000n },
          ],
        },
      }),
    );
    const history = r.histories[0];
    expect(history?.map((m) => m.senderName)).toEqual(["A", "B"]);
    expect(history?.map((m) => m.text)).toEqual(["eins", "zwei"]);
    expect(history?.map((m) => m.sentAtUnixMs)).toEqual([1730000000000n, 1730000001000n]);
  });

  it("reicht eine neue Nachricht unverändert weiter", () => {
    const r = recorder();
    const text = "<b>x</b> https://a.example  ";
    createReceiver(r.handlers).message(chat(text));
    expect(r.messages[0]?.text).toBe(text);
    expect(r.messages[0]?.senderName).toBe("A");
    expect(r.messages[0]?.sentAtUnixMs).toBe(1n);
  });

  it("meldet „zu schnell“ an onChatRejected", () => {
    const r = recorder();
    createReceiver(r.handlers).message(
      serverBytes({ case: "chatRejected", value: { reason: ChatRejected_Reason.TOO_FAST } }),
    );
    expect(r.rejections).toEqual([ChatRejected_Reason.TOO_FAST]);
    expect(r.messages).toEqual([]);
  });

  it("ignoriert einen Ablehnungsgrund ohne Bedeutung", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(serverBytes({ case: "chatRejected", value: { reason: ChatRejected_Reason.UNSPECIFIED } }));
    const unknownReason: never = 99 as never;
    receiver.message(serverBytes({ case: "chatRejected", value: { reason: unknownReason } }));
    expect(r.log).toEqual([]);
  });

  it("ignoriert unbekannte Nachrichten und bleibt danach funktionsfähig", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    expect(() => { receiver.message(new Uint8Array([0x98, 0x06, 0x01])); }).not.toThrow();
    expect(r.log).toEqual([]);
    receiver.message(chat("x"));
    expect(r.messages[0]?.text).toBe("x");
  });

  it("behält die Reihenfolge Welcome, Verlauf, neue Nachricht", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(serverBytes(welcome));
    receiver.message(serverBytes({ case: "chatHistory", value: { messages: [] } }));
    receiver.message(chat("x"));
    expect(r.log).toEqual(["welcome", "history", "message"]);
  });

  it("meldet bei leerem Verlauf nach Welcome kein Chat-Ereignis", () => {
    const r = recorder();
    createReceiver(r.handlers).message(serverBytes(welcome));
    expect(r.log).toEqual(["welcome"]);
  });

  it("Empfänger ohne Chat-Handler übersteht Chat-Nachrichten", () => {
    const receiver = createReceiver({
      onWelcome: () => {},
      onSnapshot: () => {},
      onKicked: () => {},
      onClose: () => {},
    });
    expect(() => {
      receiver.message(serverBytes({ case: "chatHistory", value: { messages: [] } }));
      receiver.message(chat("x"));
      receiver.message(serverBytes({ case: "chatRejected", value: { reason: ChatRejected_Reason.TOO_FAST } }));
    }).not.toThrow();
  });

  it("Chat ändert nichts am Verbindungsende", () => {
    const first = recorder();
    const a = createReceiver(first.handlers);
    a.message(chat("x"));
    a.close();
    expect(first.log).toEqual(["message", "close"]);

    const second = recorder();
    const b = createReceiver(second.handlers);
    b.message(chat("x"));
    b.message(serverBytes({ case: "kicked", value: { reason: Kicked_Reason.SIGNED_OUT } }));
    b.close();
    expect(second.log).toEqual(["message", "kicked"]);
  });
});

describe("connect: Chat", () => {
  beforeEach(() => {
    FakeWebSocket.instances.length = 0;
    vi.stubGlobal("WebSocket", FakeWebSocket);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sendChat schickt den Text unverändert über den offenen Socket", () => {
    const connection = connect("ws://test/ws", recorder().handlers);
    expect(connection.sendChat("  <b>x</b> ")).toBe(true);
    expect(FakeWebSocket.latest().sentChatTexts()).toEqual(["  <b>x</b> "]);
  });

  it.each([FakeWebSocket.CONNECTING, FakeWebSocket.CLOSING, FakeWebSocket.CLOSED])(
    "sendChat sendet bei nicht offener Verbindung (Zustand %i) nichts und wirft nicht",
    (state) => {
      const connection = connect("ws://test/ws", recorder().handlers);
      const socket = FakeWebSocket.latest();
      socket.readyState = state;
      expect(connection.sendChat("x")).toBe(false);
      expect(socket.sentCount()).toBe(0);
    },
  );

  it("reicht Nachrichten vom Socket an die Chat-Handler weiter", () => {
    const r = recorder();
    connect("ws://test/ws", r.handlers);
    const socket = FakeWebSocket.latest();
    socket.receive(serverBytes({ case: "chatMessage", value: { senderName: "A", text: "b", sentAtUnixMs: 1n } }));
    expect(r.messages[0]?.senderName).toBe("A");
    expect(r.messages[0]?.text).toBe("b");
    expect(socket.binaryType).toBe("arraybuffer");
  });
});
