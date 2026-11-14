import { create, fromBinary, toBinary } from "@bufbuild/protobuf";
import {
  ChatRejected_Reason,
  ClientMessageSchema,
  Kicked_Reason,
  ServerMessageSchema,
  type Attack,
  type ChatMessage,
  type KeyBindings,
  type Snapshot,
  type Welcome,
} from "@/generated/browsergame/v1/game_pb";
import type { PendingInput } from "@/features/game/state/prediction";

/** Gründe, bei denen der Server die Verbindung bewusst beendet (AK-12, AK-22). */
export type KickReason = Kicked_Reason.SIGNED_IN_ELSEWHERE | Kicked_Reason.SIGNED_OUT;

/** Gründe, aus denen der Server eine Chatnachricht ablehnt (AK-13). */
export type ChatRejectReason = ChatRejected_Reason.TOO_FAST;

export interface ConnectionHandlers {
  onWelcome(welcome: Welcome): void;
  onSnapshot(snapshot: Snapshot): void;
  onKicked(reason: KickReason): void;
  onClose(): void;
}

export interface ChatHandlers {
  onChatHistory(messages: readonly ChatMessage[]): void;
  onChatMessage(message: ChatMessage): void;
  onChatRejected(reason: ChatRejectReason): void;
}

export interface GameConnection {
  sendInput(input: PendingInput): void;
  /** Schickt den Text unverändert. `false`: Verbindung nicht offen, nichts gesendet. `true` ist keine Zustellbestätigung. */
  sendChat(text: string): boolean;
  /** Schickt die Belegung (leer = Standard). `false`: Verbindung nicht offen, nichts gesendet. */
  saveKeyBindings(keyBindings: KeyBindings): boolean;
  /** Schickt einen Angriffswunsch (013); der Server entscheidet. Bei nicht offener Verbindung geht er verloren. */
  sendAttack(attack: Attack): void;
  close(): void;
}

export function gameSocketUrl(location: Location): string {
  return `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws`;
}

export interface Receiver {
  message(data: Uint8Array): void;
  close(): void;
}

/** Leitet Server-Nachrichten weiter. Nach Kicked meldet das folgende Schließen kein onClose. */
export function createReceiver(handlers: ConnectionHandlers & Partial<ChatHandlers>): Receiver {
  let kicked = false;
  return {
    message(data) {
      const message = fromBinary(ServerMessageSchema, data);
      switch (message.payload.case) {
        case "welcome":
          handlers.onWelcome(message.payload.value);
          break;
        case "snapshot":
          handlers.onSnapshot(message.payload.value);
          break;
        case "kicked": {
          const { reason } = message.payload.value;
          if (reason === Kicked_Reason.SIGNED_IN_ELSEWHERE || reason === Kicked_Reason.SIGNED_OUT) {
            kicked = true;
            handlers.onKicked(reason);
          }
          break;
        }
        case "chatHistory":
          handlers.onChatHistory?.(message.payload.value.messages);
          break;
        case "chatMessage":
          handlers.onChatMessage?.(message.payload.value);
          break;
        case "chatRejected": {
          const { reason } = message.payload.value;
          if (reason === ChatRejected_Reason.TOO_FAST) handlers.onChatRejected?.(reason);
          break;
        }
        case undefined:
          break;
      }
    },
    close() {
      if (!kicked) handlers.onClose();
    },
  };
}

export function connect(url: string, handlers: ConnectionHandlers & ChatHandlers): GameConnection {
  const socket = new WebSocket(url);
  socket.binaryType = "arraybuffer";

  const receiver = createReceiver(handlers);
  socket.addEventListener("message", (event: MessageEvent<ArrayBuffer>) => {
    receiver.message(new Uint8Array(event.data));
  });
  socket.addEventListener("close", () => { receiver.close(); });

  return {
    sendInput(input) {
      if (socket.readyState !== WebSocket.OPEN) return;
      const message = create(ClientMessageSchema, { payload: { case: "input", value: input } });
      socket.send(toBinary(ClientMessageSchema, message));
    },
    sendChat(text) {
      if (socket.readyState !== WebSocket.OPEN) return false;
      const message = create(ClientMessageSchema, { payload: { case: "sendChat", value: { text } } });
      socket.send(toBinary(ClientMessageSchema, message));
      return true;
    },
    saveKeyBindings(keyBindings) {
      if (socket.readyState !== WebSocket.OPEN) return false;
      const message = create(ClientMessageSchema, { payload: { case: "saveKeyBindings", value: { keyBindings } } });
      socket.send(toBinary(ClientMessageSchema, message));
      return true;
    },
    sendAttack(attack) {
      if (socket.readyState !== WebSocket.OPEN) return;
      const message = create(ClientMessageSchema, { payload: { case: "attack", value: attack } });
      socket.send(toBinary(ClientMessageSchema, message));
    },
    close() {
      socket.close();
    },
  };
}
