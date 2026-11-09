import { create, fromBinary, toBinary } from "@bufbuild/protobuf";
import { ClientMessageSchema, KeyBindingsSchema, ServerMessageSchema, type KeyBindings } from "@/generated/browsergame/v1/game_pb";

type Payload = Parameters<typeof create<typeof ServerMessageSchema>>[1] extends infer I
  ? I extends { payload?: infer P } ? P : never
  : never;

export const serverBytes = (payload: Payload): Uint8Array =>
  toBinary(ServerMessageSchema, create(ServerMessageSchema, { payload }));

/** Ersatz für den Browser-WebSocket in Tests. Liefert wie der Browser nur im Zustand OPEN Nachrichten aus. */
export class FakeWebSocket extends EventTarget {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;
  static readonly instances: FakeWebSocket[] = [];

  static latest(): FakeWebSocket {
    const socket = FakeWebSocket.instances.at(-1);
    if (!socket) throw new Error("Kein WebSocket geöffnet");
    return socket;
  }

  readyState: number = FakeWebSocket.OPEN;
  binaryType = "blob";
  private readonly sent: Uint8Array[] = [];

  constructor(readonly url: string) {
    super();
    FakeWebSocket.instances.push(this);
  }

  send(data: Uint8Array): void {
    this.sent.push(data);
  }

  close(): void {
    this.readyState = FakeWebSocket.CLOSING;
  }

  receive(data: Uint8Array): void {
    if (this.readyState !== FakeWebSocket.OPEN) return;
    this.dispatchEvent(new MessageEvent("message", { data: data.slice().buffer }));
  }

  closeFromServer(): void {
    this.readyState = FakeWebSocket.CLOSED;
    this.dispatchEvent(new Event("close"));
  }

  sentChatTexts(): string[] {
    return this.sent.flatMap((data) => {
      const { payload } = fromBinary(ClientMessageSchema, data);
      return payload.case === "sendChat" ? [payload.value.text] : [];
    });
  }

  sentKeyBindings(): KeyBindings[] {
    return this.sent.flatMap((data) => {
      const { payload } = fromBinary(ClientMessageSchema, data);
      return payload.case === "saveKeyBindings" ? [payload.value.keyBindings ?? create(KeyBindingsSchema)] : [];
    });
  }

  sentInputs(): { dirX: number; dirY: number }[] {
    return this.sent.flatMap((data) => {
      const { payload } = fromBinary(ClientMessageSchema, data);
      return payload.case === "input" ? [{ dirX: payload.value.dirX, dirY: payload.value.dirY }] : [];
    });
  }

  sentCount(): number {
    return this.sent.length;
  }
}
