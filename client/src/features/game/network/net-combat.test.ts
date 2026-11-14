import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { create } from "@bufbuild/protobuf";
import { Attack_Kind, AttackSchema, TargetState_Kind, type Snapshot } from "@/generated/browsergame/v1/game_pb";
import { connect, createReceiver, type ChatHandlers, type ConnectionHandlers } from "./net";
import { FakeWebSocket, serverBytes } from "./fakeWebSocket";

function recorder() {
  const snapshots: Snapshot[] = [];
  const handlers = {
    onWelcome: () => {},
    onSnapshot: (s) => { snapshots.push(s); },
    onKicked: () => {},
    onClose: () => {},
    onChatHistory: () => {},
    onChatMessage: () => {},
    onChatRejected: () => {},
  } satisfies ConnectionHandlers & ChatHandlers;
  return { handlers, snapshots };
}

beforeEach(() => {
  FakeWebSocket.instances.length = 0;
  vi.stubGlobal("WebSocket", FakeWebSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("connect: Angriff (013)", () => {
  it("AK-7: sendAttack schickt Art und Ziel über den offenen Socket", () => {
    const connection = connect("ws://test/ws", recorder().handlers);
    connection.sendAttack(create(AttackSchema, { kind: Attack_Kind.RANGED, targetId: "dummy" }));
    const sent = FakeWebSocket.latest().sentAttacks();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.kind).toBe(Attack_Kind.RANGED);
    expect(sent[0]?.targetId).toBe("dummy");
  });

  it("ohne offene Verbindung wird nichts geschickt", () => {
    const connection = connect("ws://test/ws", recorder().handlers);
    FakeWebSocket.latest().readyState = FakeWebSocket.CONNECTING;
    connection.sendAttack(create(AttackSchema, { kind: Attack_Kind.MELEE, targetId: "dummy" }));
    expect(FakeWebSocket.latest().sentCount()).toBe(0);
  });
});

describe("createReceiver: Ziele und Projektile im Snapshot (013)", () => {
  it("reicht beide unverändert an onSnapshot weiter", () => {
    const r = recorder();
    createReceiver(r.handlers).message(
      serverBytes({
        case: "snapshot",
        value: {
          tick: 1,
          targets: [{ id: "dummy", kind: TargetState_Kind.TRAINING_DUMMY, x: 0, y: -128, hp: 60, maxHp: 100 }],
          projectiles: [{ id: 7, x: 1, y: 2 }],
        },
      }),
    );
    expect(r.snapshots[0]?.targets[0]).toMatchObject({ id: "dummy", x: 0, y: -128, hp: 60, maxHp: 100 });
    expect(r.snapshots[0]?.projectiles[0]).toMatchObject({ id: 7, x: 1, y: 2 });
  });
});
