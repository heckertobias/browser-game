import { describe, expect, it } from "vitest";
import { AttackRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { createReceiver, type AttackRejectReason, type ChatHandlers, type ConnectionHandlers } from "./net";
import { serverBytes } from "./fakeWebSocket";

function recorder() {
  const rejections: AttackRejectReason[] = [];
  const handlers = {
    onWelcome: () => {},
    onSnapshot: () => {},
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    onAttackRejected: (r) => { rejections.push(r); },
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, rejections };
}

describe("createReceiver: Ziel nicht in Reichweite (013)", () => {
  it("AK-5, AK-9: nicht in Reichweite wird gemeldet", () => {
    const r = recorder();
    createReceiver(r.handlers).message(
      serverBytes({ case: "attackRejected", value: { reason: AttackRejected_Reason.OUT_OF_RANGE } }),
    );
    expect(r.rejections).toEqual([AttackRejected_Reason.OUT_OF_RANGE]);
  });
});
