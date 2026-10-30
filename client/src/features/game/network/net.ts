import { create, fromBinary, toBinary } from "@bufbuild/protobuf";
import {
  ClientMessageSchema,
  Kicked_Reason,
  ServerMessageSchema,
  type Snapshot,
  type Welcome,
} from "../../../generated/browsergame/v1/game_pb";
import type { PendingInput } from "../state/prediction";

/** Gründe, bei denen der Server die Verbindung bewusst beendet (AK-12, AK-22). */
export type KickReason = Kicked_Reason.SIGNED_IN_ELSEWHERE | Kicked_Reason.SIGNED_OUT;

export interface ConnectionHandlers {
  onWelcome(welcome: Welcome): void;
  onSnapshot(snapshot: Snapshot): void;
  onKicked(reason: KickReason): void;
  onClose(): void;
}

export interface GameConnection {
  sendInput(input: PendingInput): void;
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
export function createReceiver(handlers: ConnectionHandlers): Receiver {
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
        case undefined:
          break;
      }
    },
    close() {
      if (!kicked) handlers.onClose();
    },
  };
}

export function connect(url: string, handlers: ConnectionHandlers): GameConnection {
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
    close() {
      socket.close();
    },
  };
}
