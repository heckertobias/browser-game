import { describe, expect, it } from "vitest";
import { CommandRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { CHAT_COMMAND_HINT_MS, CHAT_TOO_FAST_HINT_MS, chatReducer, initialChatState, type ChatAction, type ChatState } from "./chatLogic";

const run = (state: ChatState, ...actions: ChatAction[]) => actions.reduce(chatReducer, state);
const open = () => initialChatState(false);
const sent: ChatAction = { type: "sent", text: "/xyz" };

describe("Befehls-Hinweis im Chat-Zustand (015 AK-4)", () => {
  it("startet ohne Befehls-Hinweis", () => {
    expect(open().commandRejected).toBeNull();
  });

  it("Hinweis dauert so lange wie „zu schnell“", () => {
    expect(CHAT_COMMAND_HINT_MS).toBe(CHAT_TOO_FAST_HINT_MS);
  });

  it.each([CommandRejected_Reason.UNKNOWN_COMMAND, CommandRejected_Reason.INVALID] as const)("merkt sich den Grund %i", (reason) => {
    expect(run(open(), sent, { type: "commandRejected", reason }).commandRejected).toBe(reason);
  });

  it("lässt Feld und letzten Text unberührt", () => {
    const before = run(open(), sent, { type: "draft", text: "weiter" });
    const after = run(before, { type: "commandRejected", reason: CommandRejected_Reason.INVALID });
    expect(after.draft).toBe(before.draft);
    expect(after.lastSent).toBe(before.lastSent);
  });

  it("ein neuer Hinweis ersetzt den alten", () => {
    const state = run(
      open(),
      { type: "commandRejected", reason: CommandRejected_Reason.UNKNOWN_COMMAND },
      { type: "commandRejected", reason: CommandRejected_Reason.INVALID },
    );
    expect(state.commandRejected).toBe(CommandRejected_Reason.INVALID);
  });

  it("commandHintOver nimmt den Hinweis weg", () => {
    const state = run(
      open(),
      { type: "commandRejected", reason: CommandRejected_Reason.INVALID },
      { type: "commandHintOver" },
    );
    expect(state.commandRejected).toBeNull();
  });

  it("immer nur ein Hinweis", () => {
    const first = run(open(), { type: "rejected" }, { type: "commandRejected", reason: CommandRejected_Reason.INVALID });
    expect(first.tooFast).toBe(false);
    expect(first.commandRejected).toBe(CommandRejected_Reason.INVALID);
    const second = run(open(), { type: "commandRejected", reason: CommandRejected_Reason.INVALID }, { type: "rejected" });
    expect(second.commandRejected).toBeNull();
    expect(second.tooFast).toBe(true);
  });
});
