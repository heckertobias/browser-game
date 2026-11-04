import { createElement, createRef } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { ChatView, type ChatViewProps } from "./Chat";
import { CHAT_KEYBOARD_VAR, blocksEnterInField, ignoresEnterInField, initialChatState, keyboardInset, liftWhileFocused } from "./chatLogic";

const fakeViewport = (height: number, offsetTop = 0) => Object.assign(new EventTarget(), { height, offsetTop });
const setup = (innerHeight = 844, focused = false, height: number | null = 844) => {
  const field = new EventTarget();
  const viewport = fakeViewport(height ?? 0);
  const win = { innerHeight, visualViewport: height === null ? null : viewport };
  const apply = vi.fn<(insetPx: number) => void>();
  return { field, win, viewport, apply, stop: liftWhileFocused(field, focused, win, apply) };
};
const fire = (target: EventTarget, type: string) => { target.dispatchEvent(new Event(type)); };

const noop = () => {};
function view(collapsed: boolean): string {
  const props: ChatViewProps = {
    state: initialChatState(collapsed),
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
const openingTag = (markup: string, id: string) => {
  const tag = new RegExp(`<[^>]*\\bdata-testid="${id}"[^>]*>`).exec(markup)?.[0];
  if (tag === undefined) throw new Error(`data-testid="${id}" fehlt`);
  return tag;
};
const classes = (tag: string) => (/\bclass="([^"]*)"/.exec(tag)?.[1] ?? "").split(/\s+/);

describe("keyboardInset (011 AK-7)", () => {
  it("rechnet verdeckte Höhe", () => {
    expect(keyboardInset(844, { height: 844, offsetTop: 0 })).toBe(0);
    expect(keyboardInset(844, { height: 508, offsetTop: 0 })).toBe(336);
    expect(keyboardInset(844, { height: 508, offsetTop: 200 })).toBe(136);
    expect(keyboardInset(844, { height: 508, offsetTop: 336 })).toBe(0);
  });

  it("zählt nicht doppelt, wenn das Layout mitschrumpft", () => {
    expect(keyboardInset(508, { height: 508, offsetTop: 0 })).toBe(0);
  });

  it("begrenzt nach unten und rundet", () => {
    expect(keyboardInset(800, { height: 820, offsetTop: 0 })).toBe(0);
    expect(keyboardInset(844, { height: 507.6, offsetTop: 0 })).toBe(336);
    expect(keyboardInset(844, { height: 843.7, offsetTop: 0 })).toBe(0);
  });
});

describe("liftWhileFocused (011 AK-7)", () => {
  it("bleibt ohne Fokus still", () => {
    const { apply, viewport } = setup(844, false, 508);
    fire(viewport, "resize");
    fire(viewport, "scroll");
    expect(apply).not.toHaveBeenCalled();
  });

  it("startet beim Fokus", () => {
    const { apply, field } = setup(844, false, 508);
    fire(field, "focus");
    expect(apply).toHaveBeenCalledExactlyOnceWith(336);
  });

  it("folgt resize und scroll", () => {
    const { apply, field, viewport } = setup(844, false, 508);
    fire(field, "focus");
    viewport.height = 400;
    fire(viewport, "resize");
    expect(apply).toHaveBeenLastCalledWith(444);
    viewport.offsetTop = 100;
    fire(viewport, "scroll");
    expect(apply).toHaveBeenLastCalledWith(344);
  });

  it("liest die aktuelle Höhe", () => {
    const { apply, field, viewport, win } = setup(844, false, 508);
    fire(field, "focus");
    win.innerHeight = 600;
    fire(viewport, "resize");
    expect(apply).toHaveBeenLastCalledWith(92);
  });

  it("setzt bei blur zurück", () => {
    const { apply, field, viewport } = setup(844, false, 508);
    fire(field, "focus");
    fire(field, "blur");
    expect(apply).toHaveBeenLastCalledWith(0);
    const calls = apply.mock.calls.length;
    fire(viewport, "resize");
    fire(viewport, "scroll");
    expect(apply).toHaveBeenCalledTimes(calls);
  });

  it("startet bei erneutem Fokus wieder", () => {
    const { apply, field, viewport } = setup(844, false, 508);
    fire(field, "focus");
    fire(field, "blur");
    fire(field, "focus");
    expect(apply).toHaveBeenLastCalledWith(336);
    const calls = apply.mock.calls.length;
    fire(viewport, "resize");
    expect(apply).toHaveBeenCalledTimes(calls + 1);
  });

  it("hängt bei doppeltem Fokus nichts doppelt an", () => {
    const { apply, field, viewport } = setup(844, false, 508);
    fire(field, "focus");
    fire(field, "focus");
    const calls = apply.mock.calls.length;
    fire(viewport, "resize");
    expect(apply).toHaveBeenCalledTimes(calls + 1);
  });

  it("startet sofort, wenn das Feld schon fokussiert ist", () => {
    const { apply } = setup(844, true, 508);
    expect(apply).toHaveBeenCalledExactlyOnceWith(336);
  });

  it("räumt mit Fokus auf", () => {
    const { apply, field, viewport, stop } = setup(844, true, 508);
    stop();
    expect(apply).toHaveBeenLastCalledWith(0);
    const calls = apply.mock.calls.length;
    fire(viewport, "resize");
    fire(field, "focus");
    expect(apply).toHaveBeenCalledTimes(calls);
  });

  it("räumt ohne Fokus still auf", () => {
    const { apply, stop } = setup(844, false, 508);
    stop();
    expect(apply).not.toHaveBeenCalled();
  });

  it("blur ohne Fokus meldet nichts", () => {
    const { apply, field } = setup(844, false, 508);
    fire(field, "blur");
    expect(apply).not.toHaveBeenCalled();
  });

  it("wirft ohne visualViewport nicht", () => {
    const { apply, field, stop } = setup(844, false, null);
    expect(() => {
      fire(field, "focus");
      fire(field, "blur");
      stop();
    }).not.toThrow();
    expect(apply).not.toHaveBeenCalled();
  });
});

/* eslint-disable @typescript-eslint/no-deprecated -- keyCode ist bei Safari-IME das einzige Merkmal */
describe("blocksEnterInField (011 AK-5, AK-7)", () => {
  const combos = [
    [false, 13],
    [true, 13],
    [false, 229],
    [true, 229],
  ] as const;

  it("verwirft mit Touch nur gehaltenes Enter", () => {
    for (const [isComposing, keyCode] of combos) {
      expect(blocksEnterInField({ repeat: false, isComposing, keyCode }, true)).toBe(false);
      expect(blocksEnterInField({ repeat: true, isComposing, keyCode }, true)).toBe(true);
    }
  });

  it("bleibt mit feinem Zeiger wie ignoresEnterInField", () => {
    for (const [isComposing, keyCode] of combos) {
      const e = { repeat: false, isComposing, keyCode };
      expect(blocksEnterInField(e, false)).toBe(ignoresEnterInField(e));
    }
    expect(blocksEnterInField({ repeat: false, isComposing: false, keyCode: 13 }, false)).toBe(false);
    expect(blocksEnterInField({ repeat: false, isComposing: true, keyCode: 13 }, false)).toBe(true);
    expect(blocksEnterInField({ repeat: false, isComposing: false, keyCode: 229 }, false)).toBe(true);
    expect(blocksEnterInField({ repeat: true, isComposing: false, keyCode: 13 }, false)).toBe(true);
  });
});
/* eslint-enable @typescript-eslint/no-deprecated */

describe("Chat-Markup für Handy (011 AK-3, AK-7)", () => {
  it("Feld", () => {
    const tag = openingTag(view(false), "chat-input");
    expect(tag).toContain('enterKeyHint="send"');
    expect(classes(tag)).toEqual(expect.arrayContaining(["select-text", "text-base", "min-h-11"]));
  });

  it("Formular enthält Feld und Senden-Knopf", () => {
    const markup = view(false);
    const form = markup.indexOf("<form");
    const input = markup.indexOf('data-testid="chat-input"');
    const submit = markup.indexOf('type="submit"');
    expect(form).toBeGreaterThanOrEqual(0);
    expect(form).toBeLessThan(input);
    expect(input).toBeLessThan(submit);
    expect(markup.slice(form, submit)).not.toContain("</form>");
  });

  it("Liste", () => {
    expect(classes(openingTag(view(false), "chat-list"))).toEqual(
      expect.arrayContaining(["touch-pan-y", "overscroll-contain", "overflow-y-auto"]),
    );
  });

  it.each([false, true])("Wurzel hebt sich mit der Tastatur (eingeklappt=%s)", (collapsed) => {
    const list = classes(openingTag(view(collapsed), "chat"));
    const variable = `var(${CHAT_KEYBOARD_VAR},0px)`;
    const plain = list.find((c) => c.startsWith("bottom-["));
    const narrow = list.find((c) => c.startsWith("max-sm:bottom-["));
    expect(plain?.startsWith("bottom-[max(")).toBe(true);
    expect(plain?.endsWith(`,${variable})]`)).toBe(true);
    expect(narrow?.startsWith("max-sm:bottom-[max(")).toBe(true);
    expect(narrow?.endsWith(`,${variable})]`)).toBe(true);
  });
});

interface TreeProps { "data-testid"?: string; onMouseDown?: (e: { preventDefault(): void }) => void; children?: unknown }

function findByTestId(node: unknown, id: string): TreeProps | null {
  if (Array.isArray(node)) {
    for (const child of node) {
      const found = findByTestId(child, id);
      if (found) return found;
    }
    return null;
  }
  if (typeof node !== "object" || node === null || !("props" in node)) return null;
  const props = (node as { props: TreeProps }).props;
  return props["data-testid"] === id ? props : findByTestId(props.children, id);
}

describe("Knöpfe lassen dem Feld den Fokus (011 AK-7)", () => {
  it.each(["chat-collapse", "chat-send"])("%s: mousedown wird verhindert", (id) => {
    const props: ChatViewProps = {
      state: initialChatState(false),
      listRef: createRef(),
      inputRef: createRef(),
      onListScroll: noop,
      onCollapsedChange: noop,
      onDraft: noop,
      onSubmit: noop,
      onInputKeyDown: noop,
    };
    const button = findByTestId(ChatView(props), id);
    const preventDefault = vi.fn();
    button?.onMouseDown?.({ preventDefault });
    expect(preventDefault).toHaveBeenCalledOnce();
  });
});
