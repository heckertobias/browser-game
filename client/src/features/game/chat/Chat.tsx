import { memo, useCallback, useEffect, useImperativeHandle, useLayoutEffect, useReducer, useRef, useState, type KeyboardEvent, type Ref, type RefObject } from "react";
import { flushSync } from "react-dom";
import type { ChatMessage } from "@/generated/browsergame/v1/game_pb";
import type { ChatHandlers } from "@/features/game/network/net";
import { language, t } from "@/features/localization/i18n";
import {
  CHAT_FLUSH_MS,
  CHAT_MAX_LENGTH,
  CHAT_MAX_LINES,
  CHAT_TOO_FAST_HINT_MS,
  MessageBatcher,
  chatReducer,
  chatStorage,
  deviceDefaultCollapsed,
  deviceHasCoarsePointer,
  formatTime,
  handleChatEnter,
  ignoresEnterInField,
  initialChatState,
  isAtBottom,
  isComposingKey,
  leaveFocus,
  loadCollapsed,
  saveCollapsed,
  submitChat,
  timeFormatter,
  type ChatLine,
  type ChatState,
} from "./chatLogic";

const TIME_FORMAT = timeFormatter(language);

// max-sm: Auf schmalen Schirmen wäre sonst die Versionsanzeige unten rechts im Weg.
const CHAT_ROOT =
  "pointer-events-auto absolute bottom-[max(0.5rem,env(safe-area-inset-bottom))] left-[max(0.5rem,env(safe-area-inset-left))] max-sm:bottom-[calc(max(0.5rem,env(safe-area-inset-bottom))+1.25rem)] normal-case";
const CHAT_PANEL =
  "flex h-1/3 min-h-40 w-[min(24rem,calc(100%-max(0.5rem,env(safe-area-inset-left))-max(0.5rem,env(safe-area-inset-right))))] flex-col bg-black/50";

export interface ChatFeed extends ChatHandlers {
  onWelcome(): void;
}

export interface ChatProps {
  ref?: Ref<ChatFeed>;
  sendChat: (text: string) => boolean;
  inGame: boolean;
}

export const ChatRow = memo(function ChatRow({ line }: { line: ChatLine }) {
  return (
    <li data-testid="chat-line" className="whitespace-pre-wrap wrap-anywhere">
      <span className="opacity-60">{formatTime(line.message.sentAtUnixMs, TIME_FORMAT)}</span>{" "}
      <span className="font-bold">{line.message.senderName}:</span>{" "}
      <span>{line.message.text}</span>
    </li>
  );
});

export interface ChatViewProps {
  state: ChatState;
  listRef: RefObject<HTMLOListElement | null>;
  inputRef: RefObject<HTMLInputElement | null>;
  onListScroll: () => void;
  onCollapsedChange: (collapsed: boolean) => void;
  onDraft: (text: string) => void;
  onSubmit: () => void;
  onInputKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void;
}

export function ChatView({ state, listRef, inputRef, onListScroll, onCollapsedChange, onDraft, onSubmit, onInputKeyDown }: ChatViewProps) {
  if (state.collapsed) {
    return (
      <div className={CHAT_ROOT} data-testid="chat">
        <button
          type="button"
          data-testid="chat-open"
          className="btn min-h-11 rounded-none font-mono"
          onClick={() => { onCollapsedChange(false); }}
        >
          {t.chatOpen}
          {state.unread && (
            <span data-testid="chat-unread" role="img" aria-label={t.chatUnread} className="inline-block size-2.5 rounded-full bg-error" />
          )}
        </button>
      </div>
    );
  }
  return (
    <div className={`${CHAT_ROOT} ${CHAT_PANEL}`} data-testid="chat">
      <div className="flex justify-end">
        <button
          type="button"
          data-testid="chat-collapse"
          aria-label={t.chatCollapse}
          title={t.chatCollapse}
          className="btn btn-ghost btn-xs rounded-none font-mono pointer-coarse:min-h-11 pointer-coarse:min-w-11"
          onClick={() => { onCollapsedChange(true); }}
        >
          −
        </button>
      </div>
      <ol ref={listRef} data-testid="chat-list" onScroll={onListScroll} className="min-h-0 flex-1 list-none overflow-y-auto px-2">
        {state.lines.map((line) => <ChatRow key={line.id} line={line} />)}
      </ol>
      {state.tooFast && <p role="status" data-testid="chat-too-fast" className="px-2 text-warning">{t.chatTooFast}</p>}
      <form
        className="flex gap-1 p-1"
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit();
        }}
      >
        <input
          ref={inputRef}
          type="text"
          data-testid="chat-input"
          className="input min-h-11 min-w-0 flex-1 rounded-none bg-black/30 text-base"
          maxLength={CHAT_MAX_LENGTH}
          autoComplete="off"
          placeholder={t.chatPlaceholder}
          aria-label={t.chatPlaceholder}
          value={state.draft}
          onChange={(e) => { onDraft(e.target.value); }}
          onKeyDown={onInputKeyDown}
        />
        {/* Der Klick nimmt dem Feld den Fokus nicht: Am Handy bleibt die Tastatur offen. Das Verlassen regelt submitChat. */}
        <button type="submit" data-testid="chat-send" className="btn min-h-11 rounded-none font-mono" onMouseDown={(e) => { e.preventDefault(); }}>
          {t.chatSend}
        </button>
      </form>
    </div>
  );
}

export const Chat = memo(function Chat({ ref, sendChat, inGame }: ChatProps) {
  const [state, dispatch] = useReducer(chatReducer, null, () => initialChatState(loadCollapsed(chatStorage(), deviceDefaultCollapsed)));
  const listRef = useRef<HTMLOListElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const stickToBottom = useRef(true);
  const tooFastTimer = useRef<number | undefined>(undefined);
  const [batcher] = useState(
    () => new MessageBatcher<ChatMessage>((messages) => { dispatch({ type: "messages", messages }); }, CHAT_FLUSH_MS, CHAT_MAX_LINES),
  );
  const { collapsed } = state;

  useImperativeHandle(
    ref,
    () => ({
      onWelcome() {
        batcher.clear();
        stickToBottom.current = true;
        dispatch({ type: "welcome" });
      },
      onChatHistory(messages) {
        stickToBottom.current = true;
        dispatch({ type: "history", messages });
      },
      onChatMessage(message) {
        batcher.add(message);
      },
      onChatRejected() {
        dispatch({ type: "rejected" });
        window.clearTimeout(tooFastTimer.current);
        tooFastTimer.current = window.setTimeout(() => { dispatch({ type: "tooFastOver" }); }, CHAT_TOO_FAST_HINT_MS);
      },
    }),
    [batcher],
  );

  useEffect(() => () => {
    batcher.clear();
    window.clearTimeout(tooFastTimer.current);
  }, [batcher]);

  const scrollToEndIfStuck = useCallback(() => {
    const list = listRef.current;
    if (list && stickToBottom.current) list.scrollTop = list.scrollHeight;
  }, []);
  // Layout-Effekt: sonst blitzt vor dem Malen kurz die alte Scrollposition auf.
  useLayoutEffect(() => { scrollToEndIfStuck(); }, [state.lines, collapsed, scrollToEndIfStuck]);
  // Schrumpft die Liste (Hinweis, Drehen, Tastatur), bleibt die letzte Zeile sichtbar.
  useEffect(() => {
    const list = listRef.current;
    if (!list) return;
    const observer = new ResizeObserver(() => { scrollToEndIfStuck(); });
    observer.observe(list);
    return () => { observer.disconnect(); };
  }, [collapsed, scrollToEndIfStuck]);

  const setCollapsed = useCallback((value: boolean) => {
    if (!value) stickToBottom.current = true;
    saveCollapsed(chatStorage(), value);
    dispatch({ type: "setCollapsed", collapsed: value });
  }, []);

  useEffect(() => {
    if (!inGame) return;
    const abort = new AbortController();
    window.addEventListener(
      "keydown",
      (e) => {
        handleChatEnter(e, () => {
          if (collapsed) flushSync(() => { setCollapsed(false); });
          inputRef.current?.focus();
        });
      },
      { signal: abort.signal },
    );
    return () => { abort.abort(); };
  }, [inGame, collapsed, setCollapsed]);

  const onListScroll = () => {
    const list = listRef.current;
    if (list) stickToBottom.current = isAtBottom(list.scrollTop, list.scrollHeight, list.clientHeight);
  };
  const onSubmit = () => {
    const { action, leaveField } = submitChat(state.draft, deviceHasCoarsePointer(), sendChat);
    if (action) dispatch(action);
    if (leaveField) leaveFocus(document);
  };
  const onInputKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Escape") {
      // Auch bei IME: Esc darf kein Menü öffnen (AK-6). Dort bricht Esc nur die Komposition ab.
      e.stopPropagation();
      if (isComposingKey(e.nativeEvent)) return;
      dispatch({ type: "cancel" });
      e.currentTarget.blur();
    } else if (e.key === "Enter" && ignoresEnterInField(e.nativeEvent)) {
      e.preventDefault();
    }
  };

  return (
    <ChatView
      state={state}
      listRef={listRef}
      inputRef={inputRef}
      onListScroll={onListScroll}
      onCollapsedChange={setCollapsed}
      onDraft={(text) => { dispatch({ type: "draft", text }); }}
      onSubmit={onSubmit}
      onInputKeyDown={onInputKeyDown}
    />
  );
});
