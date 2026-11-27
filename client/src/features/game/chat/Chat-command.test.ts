import { createElement, createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { CommandRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import { t } from "@/features/localization/i18n";
import { Chat, ChatView, type ChatViewProps } from "./Chat";
import { chatReducer, initialChatState, type ChatAction, type ChatState } from "./chatLogic";

const noop = () => {};
/** Wie React den Text im Markup schreibt (z. B. Apostroph als &#x27;). */
const escaped = (text: string) => renderToStaticMarkup(createElement("span", null, text)).replace(/^<span>|<\/span>$/g, "");

function view(state: ChatState): string {
  const props: ChatViewProps = {
    state,
    listRef: createRef(),
    inputRef: createRef(),
    onListScroll: noop,
    onCollapsedChange: noop,
    onDraft: noop,
    onSubmit: noop,
    onInputKeyDown: noop,
  };
  return renderToStaticMarkup(createElement(ChatView, props));
}

const run = (state: ChatState, ...actions: ChatAction[]) => actions.reduce(chatReducer, state);
const open = () => initialChatState(false);
const HINT = 'data-testid="chat-command-hint"';

describe("ChatView: Befehls-Hinweis (015 AK-4)", () => {
  it("zeigt ohne Ablehnung keinen Befehls-Hinweis", () => {
    expect(view(open())).not.toContain(HINT);
  });

  it("unbekannter Befehl", () => {
    const markup = view(run(open(), { type: "commandRejected", reason: CommandRejected_Reason.UNKNOWN_COMMAND }));
    expect(markup).toContain(HINT);
    expect(markup).toContain(escaped(t.commandUnknown));
    expect(markup).not.toContain(escaped(t.commandInvalid));
  });

  it("fehlerhafter Befehl", () => {
    const markup = view(run(open(), { type: "commandRejected", reason: CommandRejected_Reason.INVALID }));
    expect(markup).toContain(HINT);
    expect(markup).toContain(escaped(t.commandInvalid));
    expect(markup).not.toContain(escaped(t.commandUnknown));
  });

  it("Hinweis über dem Eingabefeld", () => {
    const markup = view(run(open(), { type: "commandRejected", reason: CommandRejected_Reason.INVALID }));
    expect(markup.indexOf(HINT)).toBeLessThan(markup.indexOf('data-testid="chat-input"'));
  });

  it("ohne Hinweis nach commandHintOver", () => {
    const markup = view(
      run(open(), { type: "commandRejected", reason: CommandRejected_Reason.INVALID }, { type: "commandHintOver" }),
    );
    expect(markup).not.toContain(HINT);
  });

  it("Chat ohne Browser", () => {
    expect(() => renderToStaticMarkup(createElement(Chat, { sendChat: () => false, inGame: true }))).not.toThrow();
  });
});
