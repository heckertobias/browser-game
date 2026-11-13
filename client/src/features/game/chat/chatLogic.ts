import { isEditableTarget } from "@/features/game/input/input";
import type { ChatMessage } from "@/generated/browsergame/v1/game_pb";

export const CHAT_MAX_LENGTH = 200;
export const CHAT_MAX_LINES = 200;
export const CHAT_FLUSH_MS = 100;
export const CHAT_TOO_FAST_HINT_MS = 4000;
export const CHAT_COLLAPSED_KEY = "bg.chat.collapsed";
const AT_BOTTOM_TOLERANCE_PX = 8;
export const CHAT_KEYBOARD_VAR = "--chat-keyboard";

/** Ohne `timeZone`: Ortszeit. `hourCycle`: sonst zeigt `en` „02:05 PM“ statt Stunden:Minuten (AK-2). */
export function timeFormatter(language: string, timeZone?: string): Intl.DateTimeFormat {
  return new Intl.DateTimeFormat(language, { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone });
}

export function formatTime(sentAtUnixMs: bigint, formatter: Intl.DateTimeFormat): string {
  return formatter.format(Number(sentAtUnixMs));
}

export function isAtBottom(scrollTop: number, scrollHeight: number, clientHeight: number): boolean {
  return scrollHeight - scrollTop - clientHeight <= AT_BOTTOM_TOLERANCE_PX;
}

export function defaultCollapsed(isPortrait: boolean, isCoarsePointer: boolean): boolean {
  return isPortrait && isCoarsePointer;
}

export type ChatStorage = Pick<Storage, "getItem" | "setItem">;

/** `null`, wenn es keinen Speicher gibt oder der Zugriff verboten ist. */
export function chatStorage(): ChatStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/** Dieselbe Quelle für den Standard (AK-4c) und den Fokus nach dem Absenden. Ohne `matchMedia`: feiner Zeiger. */
export function deviceHasCoarsePointer(): boolean {
  try {
    return window.matchMedia("(pointer: coarse)").matches;
  } catch {
    return false;
  }
}

export function deviceDefaultCollapsed(): boolean {
  try {
    return defaultCollapsed(window.matchMedia("(orientation: portrait)").matches, deviceHasCoarsePointer());
  } catch {
    return false;
  }
}

export function loadCollapsed(storage: ChatStorage | null, fallback: () => boolean): boolean {
  let stored: string | null = null;
  try {
    stored = storage?.getItem(CHAT_COLLAPSED_KEY) ?? null;
  } catch {
    /* wie nicht gespeichert */
  }
  return stored === "true" ? true : stored === "false" ? false : fallback();
}

export function saveCollapsed(storage: ChatStorage | null, collapsed: boolean): void {
  try {
    storage?.setItem(CHAT_COLLAPSED_KEY, String(collapsed));
  } catch {
    /* gilt dann nur bis zum Neuladen */
  }
}

/** Der Text zum Senden, oder `null`, wenn nichts gesendet werden darf (AK-8, AK-9). */
export function chatTextToSend(draft: string): string | null {
  const text = draft.trim();
  return text === "" || text.length > CHAT_MAX_LENGTH ? null : text;
}

export interface SubmitResult {
  readonly action: ChatAction | null;
  readonly leaveField: boolean;
}

/** Enter oder Senden-Button. Feiner Zeiger: zurück ins Spiel, damit WASD sofort steuern. Touch: Tastatur bleibt offen. */
export function submitChat(draft: string, isCoarsePointer: boolean, sendChat: (text: string) => boolean): SubmitResult {
  const text = chatTextToSend(draft);
  if (text !== null) {
    const sent = sendChat(text);
    return { action: sent ? { type: "sent", text } : null, leaveField: sent && !isCoarsePointer };
  }
  const leave = draft.trim() === "" && !isCoarsePointer;
  return { action: leave ? { type: "cancel" } : null, leaveField: leave };
}

/** Nimmt den Fokus von Feld oder Senden-Button; bliebe er auf dem Button, finge der das nächste Enter. */
export function leaveFocus(doc: Pick<Document, "activeElement">): void {
  (doc.activeElement as HTMLElement | null)?.blur();
}

const ACTIVATABLE_TAGS: ReadonlySet<string> = new Set(["A", "BUTTON"]);

/** Globales Enter öffnet den Chat (AK-5). Buttons und Links behalten ihr eigenes Enter. */
export function shouldOpenChat(
  e: Pick<KeyboardEvent, "key" | "repeat" | "isComposing" | "altKey" | "ctrlKey" | "metaKey" | "defaultPrevented" | "target">,
): boolean {
  if (e.key !== "Enter" || e.repeat || e.isComposing || e.altKey || e.ctrlKey || e.metaKey || e.defaultPrevented) return false;
  const tag = (e.target as Partial<HTMLElement> | null)?.tagName ?? "";
  return !isEditableTarget(e.target) && !ACTIVATABLE_TAGS.has(tag);
}

/** `preventDefault`: Sonst löst dasselbe Enter im frisch fokussierten Feld das Absenden aus. */
export function handleChatEnter(
  e: Parameters<typeof shouldOpenChat>[0] & Pick<KeyboardEvent, "preventDefault">,
  open: () => void,
): void {
  if (!shouldOpenChat(e)) return;
  e.preventDefault();
  open();
}

/** Safari meldet das bestätigende Enter nach `compositionend` mit `isComposing=false` und nur `keyCode` 229. */
export function isComposingKey(e: Pick<KeyboardEvent, "isComposing" | "keyCode">): boolean {
  // oxlint-disable-next-line typescript/no-deprecated -- nur so erkennbar (Safari, IME)
  return e.isComposing || e.keyCode === 229;
}

/** Enter im Feld ohne Absenden: IME bestätigt nur; gehaltenes Enter schickte sonst einen Entwurf ab. */
export function ignoresEnterInField(e: Pick<KeyboardEvent, "repeat" | "isComposing" | "keyCode">): boolean {
  return e.repeat || isComposingKey(e);
}

/** Touch: Der Browser entscheidet. „Senden“ kommt teils mit `keyCode` 229 oder während der Komposition; reines IME-Bestätigen sendet dort von selbst nicht (AK-7). */
export function blocksEnterInField(e: Parameters<typeof ignoresEnterInField>[0], isCoarsePointer: boolean): boolean {
  return isCoarsePointer ? e.repeat : ignoresEnterInField(e);
}

/** Vom unteren Rand des Layout-Viewports verdeckte Höhe in CSS-Pixeln. `offsetTop`: Was der Browser selbst verschiebt, zählt nicht doppelt. */
export function keyboardInset(innerHeight: number, viewport: Pick<VisualViewport, "height" | "offsetTop">): number {
  return Math.max(0, Math.round(innerHeight - viewport.height - viewport.offsetTop));
}

export interface KeyboardWindow {
  readonly innerHeight: number;
  readonly visualViewport: (EventTarget & Pick<VisualViewport, "height" | "offsetTop">) | null;
}

/** Meldet die verdeckte Höhe, solange das Feld den Fokus hat; danach 0. Die Rückgabe entfernt alle Listener. */
export function liftWhileFocused(field: EventTarget, focused: boolean, win: KeyboardWindow, apply: (insetPx: number) => void): () => void {
  const fieldListeners = new AbortController();
  let viewportListeners: AbortController | null = null;
  const start = () => {
    const viewport = win.visualViewport;
    if (!viewport || viewportListeners) return;
    const listeners = new AbortController();
    viewportListeners = listeners;
    const update = () => { apply(keyboardInset(win.innerHeight, viewport)); };
    viewport.addEventListener("resize", update, { signal: listeners.signal });
    viewport.addEventListener("scroll", update, { signal: listeners.signal });
    update();
  };
  const end = () => {
    if (!viewportListeners) return;
    viewportListeners.abort();
    viewportListeners = null;
    apply(0);
  };
  field.addEventListener("focus", start, { signal: fieldListeners.signal });
  field.addEventListener("blur", end, { signal: fieldListeners.signal });
  if (focused) start();
  return () => {
    fieldListeners.abort();
    end();
  };
}

export interface ChatLine {
  readonly id: number;
  readonly message: ChatMessage;
}

export interface ChatState {
  readonly lines: readonly ChatLine[];
  readonly nextId: number;
  readonly collapsed: boolean;
  readonly unread: boolean;
  readonly draft: string;
  readonly lastSent: string;
  readonly tooFast: boolean;
}

export type ChatAction =
  | { type: "welcome" }
  | { type: "history"; messages: readonly ChatMessage[] }
  | { type: "messages"; messages: readonly ChatMessage[] }
  | { type: "setCollapsed"; collapsed: boolean }
  | { type: "draft"; text: string }
  | { type: "sent"; text: string }
  | { type: "cancel" }
  | { type: "rejected" }
  | { type: "tooFastOver" };

export function initialChatState(collapsed: boolean): ChatState {
  return { lines: [], nextId: 1, collapsed, unread: false, draft: "", lastSent: "", tooFast: false };
}

function numbered(state: ChatState, messages: readonly ChatMessage[]): ChatLine[] {
  return messages.map((message, index) => ({ id: state.nextId + index, message }));
}

export function chatReducer(state: ChatState, action: ChatAction): ChatState {
  switch (action.type) {
    case "welcome":
      return { ...state, lines: [], unread: false };
    case "history":
      return {
        ...state,
        lines: numbered(state, action.messages).slice(-CHAT_MAX_LINES),
        nextId: state.nextId + action.messages.length,
      };
    case "messages":
      return {
        ...state,
        lines: [...state.lines, ...numbered(state, action.messages)].slice(-CHAT_MAX_LINES),
        nextId: state.nextId + action.messages.length,
        unread: state.unread || state.collapsed,
      };
    case "setCollapsed":
      return { ...state, collapsed: action.collapsed, unread: action.collapsed && state.unread };
    case "draft":
      return { ...state, draft: action.text };
    case "sent":
      return { ...state, draft: "", lastSent: action.text };
    case "cancel":
      return { ...state, draft: "" };
    case "rejected":
      return { ...state, tooFast: true, draft: state.draft === "" ? state.lastSent : state.draft, lastSent: "" };
    case "tooFastOver":
      return { ...state, tooFast: false };
  }
}

/** Sammelt und liefert gebündelt in Reihenfolge, damit 50 Nachrichten/s nicht 50 Renders/s bedeuten. */
export class MessageBatcher<T> {
  private pending: T[] = [];
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private readonly flush: (items: readonly T[]) => void,
    private readonly delayMs: number,
    private readonly maxItems: number,
  ) {}

  add(item: T): void {
    this.pending.push(item);
    if (this.pending.length > this.maxItems) this.pending.shift();
    this.timer ??= setTimeout(() => {
      this.timer = undefined;
      const items = this.pending;
      this.pending = [];
      this.flush(items);
    }, this.delayMs);
  }

  clear(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
    this.pending = [];
  }
}
