import { describe, expect, it } from "vitest";
import type { CombatEvents } from "@/generated/browsergame/v1/game_pb";
import { createReceiver, type ConnectionHandlers } from "./net";
import { serverBytes } from "./fakeWebSocket";

const base = {
  onWelcome: () => {},
  onSnapshot: () => {},
  onKicked: () => {},
  onClose: () => {},
} satisfies ConnectionHandlers;

const events = serverBytes({
  case: "combatEvents",
  value: {
    launched: [{ projectileId: 7, shooterId: "a", targetId: "dummy" }],
    hits: [{ projectileId: 7, targetId: "dummy", damage: 15 }],
  },
});

describe("createReceiver: CombatEvents (013 AK-21)", () => {
  it("reicht CombatEvents unverändert an onCombatEvents weiter", () => {
    const received: CombatEvents[] = [];
    createReceiver({ ...base, onCombatEvents: (e) => { received.push(e); } }).message(events);
    expect(received).toHaveLength(1);
    expect(received[0]?.launched[0]).toMatchObject({ projectileId: 7, shooterId: "a", targetId: "dummy" });
    expect(received[0]?.hits[0]).toMatchObject({ projectileId: 7, targetId: "dummy", damage: 15 });
  });

  it("ohne onCombatEvents-Handler wirft nichts", () => {
    expect(() => { createReceiver(base).message(events); }).not.toThrow();
  });
});
