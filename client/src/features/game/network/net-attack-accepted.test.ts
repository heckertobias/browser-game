import { describe, expect, it } from "vitest";
import type { AttackAccepted } from "@/generated/browsergame/v1/game_pb";
import { createReceiver, type ChatHandlers, type ConnectionHandlers } from "./net";
import { serverBytes } from "./fakeWebSocket";

function recorder() {
  const accepted: AttackAccepted[] = [];
  const handlers = {
    onWelcome: () => {},
    onSnapshot: () => {},
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
    onAttackAccepted: (a) => { accepted.push(a); },
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, accepted };
}

const acceptedBytes = (cooldownSeconds: number) => serverBytes({ case: "attackAccepted", value: { cooldownSeconds } });

describe("createReceiver: AttackAccepted (013)", () => {
  it("AK-22: reicht AttackAccepted an onAttackAccepted weiter", () => {
    const r = recorder();
    createReceiver(r.handlers).message(acceptedBytes(1));
    expect(r.accepted).toHaveLength(1);
    expect(r.accepted[0]?.cooldownSeconds).toBe(1);
  });

  it("ohne Handler wirft nichts", () => {
    const { onAttackAccepted: _unused, ...handlers } = recorder().handlers;
    const receiver = createReceiver(handlers);
    expect(() => { receiver.message(acceptedBytes(1)); }).not.toThrow();
  });
});
