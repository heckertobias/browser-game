import { create, toBinary } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import {
  Kicked_Reason,
  ServerMessageSchema,
  type Snapshot,
  type Welcome,
} from "../../../generated/browsergame/v1/game_pb";
import { createReceiver, type KickReason } from "./net";

type Payload = Parameters<typeof create<typeof ServerMessageSchema>>[1] extends infer I
  ? I extends { payload?: infer P } ? P : never
  : never;

const bytes = (payload: Payload) => toBinary(ServerMessageSchema, create(ServerMessageSchema, { payload }));

function recorder() {
  const welcomes: Welcome[] = [];
  const snapshots: Snapshot[] = [];
  const kicks: KickReason[] = [];
  let closes = 0;
  const receiver = createReceiver({
    onWelcome: (w) => { welcomes.push(w); },
    onSnapshot: (s) => { snapshots.push(s); },
    onKicked: (r) => { kicks.push(r); },
    onClose: () => { closes++; },
  });
  return { receiver, welcomes, snapshots, kicks, closes: () => closes };
}

describe("createReceiver", () => {
  it("Kicked (anderes Gerät) meldet den Grund und kein Schließen danach (AK-12)", () => {
    const r = recorder();
    r.receiver.message(bytes({ case: "kicked", value: { reason: Kicked_Reason.SIGNED_IN_ELSEWHERE } }));
    r.receiver.close();
    expect(r.kicks).toEqual([Kicked_Reason.SIGNED_IN_ELSEWHERE]);
    expect(r.closes()).toBe(0);
  });

  it("Kicked (abgemeldet) meldet den Grund und kein Schließen danach (AK-22)", () => {
    const r = recorder();
    r.receiver.message(bytes({ case: "kicked", value: { reason: Kicked_Reason.SIGNED_OUT } }));
    r.receiver.close();
    expect(r.kicks).toEqual([Kicked_Reason.SIGNED_OUT]);
    expect(r.closes()).toBe(0);
  });

  it("Schließen ohne Kicked bleibt ein Abbruch (AK-21, 007 AK-16)", () => {
    const r = recorder();
    r.receiver.close();
    expect(r.closes()).toBe(1);
    expect(r.kicks).toHaveLength(0);
  });

  it("Kicked ohne Grund gilt als normaler Abbruch", () => {
    const r = recorder();
    r.receiver.message(bytes({ case: "kicked", value: { reason: Kicked_Reason.UNSPECIFIED } }));
    r.receiver.close();
    expect(r.kicks).toHaveLength(0);
    expect(r.closes()).toBe(1);
  });

  it("reicht Welcome und Snapshot samt away weiter (AK-20)", () => {
    const r = recorder();
    r.receiver.message(bytes({ case: "welcome", value: { playerId: "a", name: "Tobias", speed: 100, tickRateHz: 20 } }));
    r.receiver.message(
      bytes({ case: "snapshot", value: { players: [{ id: "b", name: "Bob", x: 1, y: 2, color: 0xff0000, away: true }] } }),
    );
    expect(r.welcomes[0]?.name).toBe("Tobias");
    expect(r.snapshots[0]?.players[0]?.away).toBe(true);
  });
});
