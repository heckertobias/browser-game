import { create } from "@bufbuild/protobuf";
import { createElement, createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { ChatMessageSchema } from "@/generated/browsergame/v1/game_pb";
import { language, t } from "@/features/localization/i18n";
import { Chat, ChatRow, ChatView, type ChatViewProps } from "./Chat";
import { chatReducer, formatTime, initialChatState, submitChat, timeFormatter, type ChatAction, type ChatState } from "./chatLogic";

const msg = (text: string, senderName = "A", sentAtUnixMs = 1n) => create(ChatMessageSchema, { senderName, text, sentAtUnixMs });
const noop = () => {};

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

const row = (message = msg("hi")) => renderToStaticMarkup(createElement(ChatRow, { line: { id: 1, message } }));
const run = (state: ChatState, ...actions: ChatAction[]) => actions.reduce(chatReducer, state);
const open = () => initialChatState(false);
const collapsed = () => initialChatState(true);

describe("ChatRow (011 AK-2, AK-12)", () => {
  it("zeigt Nachrichtentext als Text", () => {
    const markup = row(msg("<img src=x onerror=alert(1)> <b>x</b> https://example.com"));
    expect(markup).toContain("&lt;img");
    expect(markup).toContain("&lt;b&gt;");
    expect(markup).toContain("https://example.com");
    expect(markup).not.toMatch(/<(img|b|a|script)\b/);
  });

  it("bricht lange Wörter um", () => {
    expect(row()).toContain("wrap-anywhere");
  });

  it("zeigt Uhrzeit, Name und Text in dieser Reihenfolge", () => {
    const sentAt = 1_700_000_000_000n;
    const markup = row(msg("der Text", "Absender", sentAt));
    const time = markup.indexOf(formatTime(sentAt, timeFormatter(language)));
    const name = markup.indexOf("Absender");
    const text = markup.indexOf("der Text");
    expect(time).toBeGreaterThanOrEqual(0);
    expect(name).toBeGreaterThan(time);
    expect(text).toBeGreaterThan(name);
  });
});

describe("ChatView", () => {
  it("zeigt die neuere Nachricht weiter unten (AK-2)", () => {
    const markup = view(run(open(), { type: "messages", messages: [msg("erste"), msg("zweite")] }));
    expect(markup.indexOf("zweite")).toBeGreaterThan(markup.indexOf("erste"));
  });

  it("setzt Text nicht in Großbuchstaben (AK-12)", () => {
    expect(view(open())).toMatch(/data-testid="chat"/);
    const tag = /<[^>]*data-testid="chat"[^>]*>/.exec(view(open()))?.[0] ?? "";
    expect(tag).toContain("normal-case");
  });

  it("zeigt offen Liste, Eingabe und Knöpfe (AK-4, AK-9)", () => {
    const markup = view(open());
    for (const id of ["chat-list", "chat-input", "chat-send", "chat-collapse"]) expect(markup).toContain(`data-testid="${id}"`);
    expect(markup).not.toContain('data-testid="chat-open"');
    const input = /<input[^>]*>/.exec(markup)?.[0] ?? "";
    expect(input).toContain('maxLength="200"');
    expect(input).toContain(`placeholder="${t.chatPlaceholder}"`);
  });

  it("zeigt eingeklappt nur den Öffnen-Knopf (AK-4)", () => {
    const markup = view(collapsed());
    expect(markup).toContain('data-testid="chat-open"');
    expect(markup).toContain(t.chatOpen);
    expect(markup).not.toContain('data-testid="chat-list"');
    expect(markup).not.toContain('data-testid="chat-input"');
    expect(markup).not.toContain('data-testid="chat-unread"');
  });

  it("zeigt den Punkt bei Ungelesenem (AK-4)", () => {
    const markup = view(run(collapsed(), { type: "messages", messages: [msg("a")] }));
    expect(markup).toContain('data-testid="chat-unread"');
    expect(markup).toContain(`aria-label="${t.chatUnread}"`);
  });

  it("zeigt den Hinweis nur bei tooFast (AK-13)", () => {
    expect(view(open())).not.toContain('data-testid="chat-too-fast"');
    const markup = view(run(open(), { type: "rejected" }));
    expect(markup).toContain('data-testid="chat-too-fast"');
    expect(markup).toContain(t.chatTooFast);
  });

  it("zeigt den wieder eingesetzten Text samt Hinweis (AK-13)", () => {
    const result = submitChat("hallo", false, () => true);
    const state = run(open(), { type: "draft", text: "hallo" }, result.action as ChatAction, { type: "rejected" });
    const markup = view(state);
    expect(/<input[^>]*>/.exec(markup)?.[0]).toContain('value="hallo"');
    expect(markup).toContain('data-testid="chat-too-fast"');
  });
});

describe("Chat ohne Browser (011 AK-4c)", () => {
  it("rendert in Node ohne window", () => {
    expect(() => renderToStaticMarkup(createElement(Chat, { sendChat: () => false, inGame: true }))).not.toThrow();
  });
});
