import { create, fromBinary, toBinary } from "@bufbuild/protobuf";
import {
  ClientMessageSchema,
  ServerMessageSchema,
  type Snapshot,
  type Welcome,
} from "../generated/browsergame/v1/game_pb";
import type { PendingInput } from "./prediction";

export interface ConnectionHandlers {
  onWelcome(welcome: Welcome): void;
  onSnapshot(snapshot: Snapshot): void;
  onClose(): void;
}

export interface GameConnection {
  sendInput(input: PendingInput): void;
  close(): void;
}

export function gameSocketUrl(location: Location): string {
  return `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws`;
}

export function connect(url: string, handlers: ConnectionHandlers): GameConnection {
  const socket = new WebSocket(url);
  socket.binaryType = "arraybuffer";

  socket.addEventListener("message", (event: MessageEvent<ArrayBuffer>) => {
    const message = fromBinary(ServerMessageSchema, new Uint8Array(event.data));
    switch (message.payload.case) {
      case "welcome":
        handlers.onWelcome(message.payload.value);
        break;
      case "snapshot":
        handlers.onSnapshot(message.payload.value);
        break;
      case undefined:
        break;
    }
  });
  socket.addEventListener("close", () => { handlers.onClose(); });

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
