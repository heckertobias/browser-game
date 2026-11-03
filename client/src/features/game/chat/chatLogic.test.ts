import { create } from "@bufbuild/protobuf";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChatMessageSchema } from "@/generated/browsergame/v1/game_pb";
import {
  CHAT_COLLAPSED_KEY,
  CHAT_FLUSH_MS,
  CHAT_MAX_LENGTH,
  CHAT_MAX_LINES,
  CHAT_TOO_FAST_HINT_MS,
  MessageBatcher,
  chatReducer,
  chatStorage,
  chatTextToSend,
  defaultCollapsed,
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
  shouldOpenChat,
  submitChat,
  timeFormatter,
  type ChatAction,
  type ChatState,
} from "./chatLogic";

const msg = (text: string, senderName = "A", sentAtUnixMs = 1n) => create(ChatMessageSchema, { senderName, text, sentAtUnixMs });

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("Konstanten", () => {
  it("haben die festgelegten Werte", () => {
    expect(CHAT_FLUSH_MS).toBe(100);
    expect(CHAT_COLLAPSED_KEY).toBe("bg.chat.collapsed");
    expect(CHAT_TOO_FAST_HINT_MS).toBe(4000);
  });
});

describe("formatTime (011 AK-2)", () => {
  it("zeigt die Ortszeit", () => {
    const at = BigInt(new Date(2026, 0, 15, 7, 5).getTime());
    expect(formatTime(at, timeFormatter("de"))).toBe("07:05");
  });

  it("nutzt die übergebene Zeitzone", () => {
    const at = BigInt(Date.UTC(2026, 0, 15, 7, 5));
    expect(formatTime(at, timeFormatter("de", "Asia/Tokyo"))).toBe("16:05");
    expect(formatTime(at, timeFormatter("de", "UTC"))).toBe("07:05");
  });

  it.each(["de", "en"])("zeigt 24 Stunden in %s", (language) => {
    const afternoon = BigInt(Date.UTC(2026, 0, 15, 14, 5));
    const midnight = BigInt(Date.UTC(2026, 0, 15, 0, 5));
    expect(formatTime(afternoon, timeFormatter(language, "UTC"))).toBe("14:05");
    expect(formatTime(midnight, timeFormatter(language, "UTC"))).toBe("00:05");
  });
});

describe("isAtBottom (011 AK-3)", () => {
  it("erkennt das Ende mit Toleranz", () => {
    expect(isAtBottom(100, 200, 100)).toBe(true);
    expect(isAtBottom(92, 200, 100)).toBe(true);
    expect(isAtBottom(91, 200, 100)).toBe(false);
    expect(isAtBottom(0, 100, 100)).toBe(true);
    expect(isAtBottom(105, 200, 100)).toBe(true);
  });
});

describe("Standard eingeklappt (011 AK-4c)", () => {
  it.each([
    [true, true, true],
    [true, false, false],
    [false, true, false],
    [false, false, false],
  ])("hochkant %s, Touch %s ergibt %s", (portrait, coarse, expected) => {
    expect(defaultCollapsed(portrait, coarse)).toBe(expected);
  });

  const stubMedia = (answers: Record<string, boolean>) => {
    vi.stubGlobal("window", { matchMedia: (query: string) => ({ matches: answers[query] ?? false }) });
  };

  it("fragt Ausrichtung und Zeiger ab", () => {
    stubMedia({ "(orientation: portrait)": true, "(pointer: coarse)": true });
    expect(deviceDefaultCollapsed()).toBe(true);
    stubMedia({ "(orientation: portrait)": true, "(pointer: coarse)": false });
    expect(deviceDefaultCollapsed()).toBe(false);
    stubMedia({ "(orientation: portrait)": false, "(pointer: coarse)": true });
    expect(deviceDefaultCollapsed()).toBe(false);
  });

  it("ist ohne Browser offen", () => {
    expect(deviceDefaultCollapsed()).toBe(false);
  });
});

describe("deviceHasCoarsePointer (011 AK-5)", () => {
  it("reicht die Abfrage durch", () => {
    vi.stubGlobal("window", { matchMedia: (query: string) => ({ matches: query === "(pointer: coarse)" }) });
    expect(deviceHasCoarsePointer()).toBe(true);
    vi.stubGlobal("window", { matchMedia: () => ({ matches: false }) });
    expect(deviceHasCoarsePointer()).toBe(false);
  });

  it("ist ohne window und ohne matchMedia ein feiner Zeiger", () => {
    expect(deviceHasCoarsePointer()).toBe(false);
    vi.stubGlobal("window", {});
    expect(deviceHasCoarsePointer()).toBe(false);
  });
});

describe("Speicher (011 AK-4b)", () => {
  const memory = (initial?: string) => {
    const values = new Map<string, string>(initial === undefined ? [] : [[CHAT_COLLAPSED_KEY, initial]]);
    return {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => { values.set(key, value); },
      values,
    };
  };
  const throwing = {
    getItem: () => { throw new Error("verboten"); },
    setItem: () => { throw new Error("verboten"); },
  };

  it("chatStorage liefert null ohne window", () => {
    expect(chatStorage()).toBeNull();
  });

  it("chatStorage liefert null, wenn der Zugriff wirft", () => {
    vi.stubGlobal("window", { get localStorage(): never { throw new Error("verboten"); } });
    expect(chatStorage()).toBeNull();
  });

  it("chatStorage liefert den Speicher", () => {
    const storage = memory();
    vi.stubGlobal("window", { localStorage: storage });
    expect(chatStorage()).toBe(storage);
  });

  it.each([true, false])("gespeicherte Werte gewinnen über den Fallback %s", (fallback) => {
    expect(loadCollapsed(memory("true"), () => fallback)).toBe(true);
    expect(loadCollapsed(memory("false"), () => fallback)).toBe(false);
  });

  it.each([true, false])("ohne brauchbaren Wert gilt der Fallback %s", (fallback) => {
    expect(loadCollapsed(memory(), () => fallback)).toBe(fallback);
    expect(loadCollapsed(memory("1"), () => fallback)).toBe(fallback);
    expect(loadCollapsed(memory("yes"), () => fallback)).toBe(fallback);
    expect(loadCollapsed(null, () => fallback)).toBe(fallback);
    expect(loadCollapsed(throwing, () => fallback)).toBe(fallback);
  });

  it("saveCollapsed schreibt unter dem Schlüssel und macht eine Rundreise", () => {
    const storage = memory();
    saveCollapsed(storage, true);
    expect(storage.values.get(CHAT_COLLAPSED_KEY)).toBe("true");
    expect(loadCollapsed(storage, () => false)).toBe(true);
    saveCollapsed(storage, false);
    expect(loadCollapsed(storage, () => true)).toBe(false);
  });

  it("saveCollapsed wirft nicht", () => {
    expect(() => { saveCollapsed(null, true); }).not.toThrow();
    expect(() => { saveCollapsed(throwing, true); }).not.toThrow();
  });
});

describe("chatTextToSend (011 AK-8, AK-9)", () => {
  it("lehnt leeren Text ab", () => {
    expect(chatTextToSend("")).toBeNull();
    expect(chatTextToSend("   ")).toBeNull();
    expect(chatTextToSend("\t\n")).toBeNull();
  });

  it("schneidet Leerraum ab", () => {
    expect(chatTextToSend(" hi ")).toBe("hi");
  });

  it("erlaubt 200 Zeichen, nicht 201", () => {
    expect(chatTextToSend("a".repeat(CHAT_MAX_LENGTH))).toBe("a".repeat(CHAT_MAX_LENGTH));
    expect(chatTextToSend("a".repeat(CHAT_MAX_LENGTH + 1))).toBeNull();
    expect(chatTextToSend(`${"a".repeat(CHAT_MAX_LENGTH)} `)).toBe("a".repeat(CHAT_MAX_LENGTH));
  });
});

describe("submitChat (011 AK-5, AK-8, AK-9)", () => {
  it("sendet getrimmt und verlässt das Feld mit feinem Zeiger", () => {
    const send = vi.fn(() => true);
    expect(submitChat(" hi ", false, send)).toEqual({ action: { type: "sent", text: "hi" }, leaveField: true });
    expect(send).toHaveBeenCalledOnce();
    expect(send).toHaveBeenCalledWith("hi");
  });

  it("bleibt mit Touch im Feld", () => {
    expect(submitChat("hi", true, () => true)).toEqual({ action: { type: "sent", text: "hi" }, leaveField: false });
  });

  it.each([true, false])("lässt Text und Fokus, wenn nicht gesendet wurde (Touch %s)", (coarse) => {
    expect(submitChat("hi", coarse, () => false)).toEqual({ action: null, leaveField: false });
  });

  it.each(["", "   "])("leer %j: sendet nichts", (draft) => {
    const send = vi.fn(() => true);
    expect(submitChat(draft, false, send)).toEqual({ action: { type: "cancel" }, leaveField: true });
    expect(submitChat(draft, true, send)).toEqual({ action: null, leaveField: false });
    expect(send).not.toHaveBeenCalled();
  });

  it.each([true, false])("zu lang: sendet nichts und bleibt (Touch %s)", (coarse) => {
    const send = vi.fn(() => true);
    expect(submitChat("a".repeat(CHAT_MAX_LENGTH + 1), coarse, send)).toEqual({ action: null, leaveField: false });
    expect(send).not.toHaveBeenCalled();
  });
});

describe("leaveFocus (011 AK-5)", () => {
  it("nimmt dem aktiven Element den Fokus", () => {
    const blur = vi.fn();
    leaveFocus({ activeElement: { blur } as unknown as Element });
    expect(blur).toHaveBeenCalledOnce();
  });

  it("wirft ohne aktives Element nicht", () => {
    expect(() => { leaveFocus({ activeElement: null }); }).not.toThrow();
  });
});

describe("Enter öffnet den Chat (011 AK-5)", () => {
  const key = (over: Partial<Parameters<typeof shouldOpenChat>[0]> & { target?: unknown } = {}) =>
    ({
      key: "Enter",
      repeat: false,
      isComposing: false,
      altKey: false,
      ctrlKey: false,
      metaKey: false,
      defaultPrevented: false,
      target: { tagName: "BODY" },
      ...over,
    }) as Parameters<typeof shouldOpenChat>[0];

  it.each(["BODY", "CANVAS"])("öffnet bei Ziel %s", (tagName) => {
    expect(shouldOpenChat(key({ target: { tagName } as unknown as EventTarget }))).toBe(true);
  });

  it("öffnet bei einem Ziel ohne Tag (window)", () => {
    expect(shouldOpenChat(key({ target: {} as unknown as EventTarget }))).toBe(true);
  });

  it.each([
    ["andere Taste", { key: "a" }],
    ["repeat", { repeat: true }],
    ["IME", { isComposing: true }],
    ["Alt", { altKey: true }],
    ["Ctrl", { ctrlKey: true }],
    ["Meta", { metaKey: true }],
    ["defaultPrevented", { defaultPrevented: true }],
    ["INPUT", { target: { tagName: "INPUT" } }],
    ["TEXTAREA", { target: { tagName: "TEXTAREA" } }],
    ["BUTTON", { target: { tagName: "BUTTON" } }],
    ["A", { target: { tagName: "A" } }],
    ["contentEditable", { target: { tagName: "DIV", isContentEditable: true } }],
  ])("öffnet nicht bei %s", (_name, over) => {
    expect(shouldOpenChat(key(over as Parameters<typeof key>[0]))).toBe(false);
  });

  it("handleChatEnter verhindert den Standard und öffnet einmal", () => {
    const preventDefault = vi.fn();
    const open = vi.fn();
    handleChatEnter({ ...key(), preventDefault }, open);
    expect(preventDefault).toHaveBeenCalledOnce();
    expect(open).toHaveBeenCalledOnce();
  });

  it.each(["INPUT", "BUTTON"])("handleChatEnter tut bei Ziel %s nichts", (tagName) => {
    const preventDefault = vi.fn();
    const open = vi.fn();
    handleChatEnter({ ...key({ target: { tagName } as unknown as EventTarget }), preventDefault }, open);
    expect(preventDefault).not.toHaveBeenCalled();
    expect(open).not.toHaveBeenCalled();
  });

  it("das sendende Enter und seine Wiederholung öffnen nicht, ein neuer Druck schon", () => {
    const open = vi.fn();
    const preventDefault = vi.fn();
    handleChatEnter({ ...key({ target: { tagName: "INPUT" } as unknown as EventTarget }), preventDefault }, open);
    handleChatEnter({ ...key({ repeat: true }), preventDefault }, open);
    expect(open).not.toHaveBeenCalled();
    handleChatEnter({ ...key(), preventDefault }, open);
    expect(open).toHaveBeenCalledOnce();
  });
});

/* eslint-disable @typescript-eslint/no-deprecated -- keyCode ist bei Safari-IME das einzige Merkmal */
describe("Enter im Feld (011 AK-5)", () => {
  it("isComposingKey erkennt IME", () => {
    expect(isComposingKey({ isComposing: true, keyCode: 13 })).toBe(true);
    expect(isComposingKey({ isComposing: false, keyCode: 229 })).toBe(true);
    expect(isComposingKey({ isComposing: false, keyCode: 13 })).toBe(false);
  });

  it("ignoresEnterInField ignoriert Wiederholung und IME", () => {
    expect(ignoresEnterInField({ repeat: true, isComposing: false, keyCode: 13 })).toBe(true);
    expect(ignoresEnterInField({ repeat: false, isComposing: true, keyCode: 13 })).toBe(true);
    expect(ignoresEnterInField({ repeat: false, isComposing: false, keyCode: 229 })).toBe(true);
    expect(ignoresEnterInField({ repeat: false, isComposing: false, keyCode: 13 })).toBe(false);
  });
});
/* eslint-enable @typescript-eslint/no-deprecated */

describe("chatReducer", () => {
  const texts = (state: ChatState) => state.lines.map((line) => line.message.text);
  const run = (state: ChatState, ...actions: ChatAction[]) => actions.reduce(chatReducer, state);
  const open = () => initialChatState(false);
  const collapsed = () => initialChatState(true);

  it("verwaltet die Liste (AK-2)", () => {
    const filled = run(open(), { type: "messages", messages: [msg("1"), msg("2")] });
    expect(texts(filled)).toEqual(["1", "2"]);
    expect(texts(run(filled, { type: "messages", messages: [msg("3")] }))).toEqual(["1", "2", "3"]);
    expect(texts(run(filled, { type: "history", messages: [msg("x"), msg("y")] }))).toEqual(["x", "y"]);
    expect(run(filled, { type: "welcome" }).lines).toEqual([]);
  });

  it("begrenzt auf 200 Zeilen mit stabilen ids (AK-3)", () => {
    const messages = Array.from({ length: 205 }, (_, i) => msg(i < 3 ? "gleich" : String(i)));
    const state = run(open(), { type: "messages", messages });
    expect(state.lines).toHaveLength(CHAT_MAX_LINES);
    expect(state.lines[0]?.message).toBe(messages[5]);
    expect(state.lines.at(-1)?.message).toBe(messages[204]);
    const ids = state.lines.map((line) => line.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.every((id, i) => i === 0 || id > (ids[i - 1] ?? 0))).toBe(true);

    const next = run(state, { type: "messages", messages: [msg("neu")] });
    expect(next.lines.at(-1)?.id).toBeGreaterThan(ids.at(-1) ?? 0);
    expect(next.lines.slice(0, -2).every((line, i) => line === state.lines[i + 1])).toBe(true);
    expect(next.lines[0]).toBe(state.lines[1]);
  });

  it("vergibt ids über history und messages hinweg eindeutig", () => {
    const state = run(open(), { type: "history", messages: [msg("a"), msg("b")] }, { type: "messages", messages: [msg("c")] });
    const ids = state.lines.map((line) => line.id);
    expect(new Set(ids).size).toBe(3);
    expect(ids).toEqual([...ids].sort((a, b) => a - b));
  });

  it("history lässt unread unverändert", () => {
    const unread = run(collapsed(), { type: "messages", messages: [msg("a")] });
    expect(run(unread, { type: "history", messages: [msg("b")] }).unread).toBe(true);
  });

  it("history begrenzt ebenfalls", () => {
    const state = run(open(), { type: "history", messages: Array.from({ length: 205 }, (_, i) => msg(String(i))) });
    expect(state.lines).toHaveLength(CHAT_MAX_LINES);
    expect(state.lines.at(-1)?.message.text).toBe("204");
  });

  it("zählt Ungelesenes nur eingeklappt (AK-4)", () => {
    expect(run(collapsed(), { type: "messages", messages: [msg("a")] }).unread).toBe(true);
    expect(run(open(), { type: "messages", messages: [msg("a")] }).unread).toBe(false);
    expect(run(collapsed(), { type: "history", messages: [msg("a")] }).unread).toBe(false);
    const unread = run(collapsed(), { type: "messages", messages: [msg("a")] });
    expect(run(unread, { type: "setCollapsed", collapsed: false }).unread).toBe(false);
    expect(run(unread, { type: "welcome" }).unread).toBe(false);
    expect(run(open(), { type: "setCollapsed", collapsed: true }).unread).toBe(false);
    expect(run(unread, { type: "setCollapsed", collapsed: true }).unread).toBe(true);
  });

  it("leert den Entwurf beim Senden und Abbrechen (AK-5, AK-13)", () => {
    const typed = run(open(), { type: "draft", text: "hallo" });
    expect(typed.draft).toBe("hallo");
    const sent = run(typed, { type: "sent", text: "hallo" });
    expect(sent.draft).toBe("");
    expect(sent.lastSent).toBe("hallo");
    expect(run(typed, { type: "cancel" }).draft).toBe("");
  });

  it("setzt bei Ablehnung den gesendeten Text wieder ein (AK-13)", () => {
    const sent = run(open(), { type: "draft", text: "hallo" }, { type: "sent", text: "hallo" });
    const rejected = run(sent, { type: "rejected" });
    expect(rejected.draft).toBe("hallo");
    expect(rejected.tooFast).toBe(true);
    expect(run(rejected, { type: "tooFastOver" }).tooFast).toBe(false);
    expect(run(rejected, { type: "tooFastOver" }).draft).toBe("hallo");
  });

  it("überschreibt Getipptes bei Ablehnung nicht", () => {
    const state = run(open(), { type: "sent", text: "alt" }, { type: "draft", text: "neu" }, { type: "rejected" });
    expect(state.draft).toBe("neu");
  });

  it("setzt nach geleertem Feld bei einer zweiten Ablehnung nichts ein", () => {
    const state = run(open(), { type: "sent", text: "alt" }, { type: "rejected" }, { type: "cancel" }, { type: "rejected" });
    expect(state.draft).toBe("");
  });

  it("setzt den Text nach dem Verlassen des Feldes wieder ein (AK-13)", () => {
    const result = submitChat("hallo", false, () => true);
    expect(result.leaveField).toBe(true);
    const state = run(open(), { type: "draft", text: "hallo" }, result.action as ChatAction, { type: "rejected" });
    expect(state.draft).toBe("hallo");
    expect(state.tooFast).toBe(true);
  });
});

describe("MessageBatcher", () => {
  const setup = (maxItems = 1000) => {
    vi.useFakeTimers();
    const flush = vi.fn<(items: readonly number[]) => void>();
    return { flush, batcher: new MessageBatcher<number>(flush, 100, maxItems) };
  };

  it("liefert nach 100 ms gebündelt in Reihenfolge", () => {
    const { flush, batcher } = setup();
    for (let i = 1; i <= 5; i++) batcher.add(i);
    vi.advanceTimersByTime(99);
    expect(flush).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(flush).toHaveBeenCalledOnce();
    expect(flush).toHaveBeenCalledWith([1, 2, 3, 4, 5]);
  });

  it("öffnet danach ein neues Fenster", () => {
    const { flush, batcher } = setup();
    batcher.add(1);
    vi.advanceTimersByTime(100);
    batcher.add(2);
    vi.advanceTimersByTime(100);
    expect(flush).toHaveBeenCalledTimes(2);
    expect(flush).toHaveBeenLastCalledWith([2]);
  });

  it("verwirft bei clear und feuert nicht", () => {
    const { flush, batcher } = setup();
    batcher.add(1);
    batcher.clear();
    vi.advanceTimersByTime(500);
    expect(flush).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    batcher.add(2);
    vi.advanceTimersByTime(100);
    expect(flush).toHaveBeenCalledOnce();
    expect(flush).toHaveBeenCalledWith([2]);
  });

  it("behält die neuesten", () => {
    const { flush, batcher } = setup(3);
    for (let i = 1; i <= 5; i++) batcher.add(i);
    vi.advanceTimersByTime(100);
    expect(flush).toHaveBeenCalledWith([3, 4, 5]);
  });

  it("macht aus 50 Nachrichten in einer Sekunde höchstens 10 Flushes", () => {
    const { flush, batcher } = setup();
    for (let i = 0; i < 50; i++) {
      batcher.add(i);
      vi.advanceTimersByTime(20);
    }
    expect(flush).toHaveBeenCalledTimes(10);
    vi.advanceTimersByTime(100);
    expect(flush).toHaveBeenCalledTimes(10);
    expect(flush.mock.calls.flatMap(([items]) => [...items])).toEqual(Array.from({ length: 50 }, (_, i) => i));
  });
});
