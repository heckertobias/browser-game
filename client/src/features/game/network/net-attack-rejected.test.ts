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

const rejected = (reason: AttackRejected_Reason) => serverBytes({ case: "attackRejected", value: { reason } });

describe("createReceiver: AttackRejected (013)", () => {
  it("AK-8: zu wenig Mana wird gemeldet", () => {
    const r = recorder();
    createReceiver(r.handlers).message(rejected(AttackRejected_Reason.NOT_ENOUGH_MANA));
    expect(r.rejections).toEqual([AttackRejected_Reason.NOT_ENOUGH_MANA]);
  });

  it("AK-19: Ziel nicht vor dir wird gemeldet", () => {
    const r = recorder();
    createReceiver(r.handlers).message(rejected(AttackRejected_Reason.NOT_FACING));
    expect(r.rejections).toEqual([AttackRejected_Reason.NOT_FACING]);
  });

  it("Gründe ohne Bedeutung werden ignoriert", () => {
    const r = recorder();
    const receiver = createReceiver(r.handlers);
    receiver.message(rejected(AttackRejected_Reason.UNSPECIFIED));
    const unknownReason: never = 99 as never;
    receiver.message(serverBytes({ case: "attackRejected", value: { reason: unknownReason } }));
    expect(r.rejections).toEqual([]);
  });

  it("ohne Handler wirft nichts", () => {
    const { onAttackRejected: _unused, ...handlers } = recorder().handlers;
    const receiver = createReceiver(handlers);
    expect(() => { receiver.message(rejected(AttackRejected_Reason.NOT_FACING)); }).not.toThrow();
  });
});
