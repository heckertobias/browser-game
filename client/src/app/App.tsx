import { useCallback, useEffect, useRef, useState } from "react";
import { Kicked_Reason, type Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { AttackButtons } from "@/features/game/combat/AttackButtons";
import { AttackHint, NO_TARGET, type HintFeed } from "@/features/game/combat/AttackHint";
import { Chat, type ChatFeed } from "@/features/game/chat/Chat";
import { Game, type ConnectionStatus, type PlayerVitals } from "@/features/game/engine/game";
import { Vitals } from "@/features/game/hud/Vitals";
import type { Bindings } from "@/features/game/input/bindings";
import { KeyBindings } from "@/features/game/menu/KeyBindings";
import { Menu } from "@/features/game/menu/Menu";
import { menuKeyAction } from "@/features/game/menu/menuLogic";
import type { KickReason } from "@/features/game/network/net";
import { t } from "@/features/localization/i18n";
import { browserReloadWindow, reloadPage } from "@/features/pwa/services/reload";
import { ServerWatch, type ServerWatchDeps } from "@/features/pwa/services/update";
import { APP_VERSION } from "@/features/pwa/services/version";

const STATUS_LABEL: Record<Exclude<ConnectionStatus, "kicked">, string> = {
  connecting: t.statusConnecting,
  connected: t.statusConnected,
  disconnected: t.statusDisconnected,
};

// Direkt zur Anmeldung statt /play neu zu laden: Nach Passwort-Reset auf einem anderen Gerät gilt das alte Cookie hier noch und führte wieder ins Spiel.
const LOGIN_URL = "/Account/Login?returnUrl=%2Fplay";

const OVERLAY =
  "pointer-events-auto absolute inset-0 flex flex-col items-center justify-center gap-4 bg-black/60 text-center pb-[max(0.5rem,env(safe-area-inset-bottom))] pl-[max(0.5rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))] pt-[max(0.5rem,env(safe-area-inset-top))]";

function currentEntryScript(): string | null {
  return document.querySelector('script[type="module"][src]')?.getAttribute("src") ?? null;
}

const browserWatchDeps = (): ServerWatchDeps => ({
  fetch: (url, init) => fetch(url, init),
  timers: {
    setTimeout: (handler, timeout) => window.setTimeout(handler, timeout),
    clearTimeout: (id) => {
      window.clearTimeout(id);
    },
  },
  doc: document,
  win: window,
});

export function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const chatRef = useRef<ChatFeed>(null);
  const hintRef = useRef<HintFeed>(null);
  const gameRef = useRef<Game | null>(null);
  const sendChat = useCallback((text: string) => gameRef.current?.sendChat(text) ?? false, []);
  const attack = useCallback((kind: Attack_Kind) => { gameRef.current?.attack(kind); }, []);
  const saveKeyBindings = useCallback((bindings: Bindings) => { gameRef.current?.saveKeyBindings(bindings); }, []);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [name, setName] = useState<string | null>(null);
  const [kicked, setKicked] = useState<KickReason | null>(null);
  const [coordinates, setCoordinates] = useState<string | null>(null);
  const [vitals, setVitals] = useState<PlayerVitals | null>(null);
  const [target, setTarget] = useState<string | null>(null);
  const [update, setUpdate] = useState(false);
  const [reload, setReload] = useState<"idle" | "pending" | "stuck">("idle");
  const [keyBindings, setKeyBindings] = useState<Bindings | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuAvailable = status === "connecting" || status === "connected";
  const menuShown = menuOpen && menuAvailable;

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const game = new Game(canvas, {
      onStatus: setStatus,
      onWelcome: (welcomeName) => {
        setName(welcomeName);
        chatRef.current?.onWelcome();
      },
      onKicked: (reason) => {
        setKicked(reason);
        if (reason === Kicked_Reason.SIGNED_OUT) window.location.replace(LOGIN_URL);
      },
      onKeyBindings: setKeyBindings,
      onCoordinates: setCoordinates,
      onVitals: setVitals,
      onTarget: setTarget,
      onAttackRejected: (reason) => { hintRef.current?.show(reason); },
      onNoTarget: () => { hintRef.current?.show(NO_TARGET); },
      onChatHistory: (messages) => { chatRef.current?.onChatHistory(messages); },
      onChatMessage: (message) => { chatRef.current?.onChatMessage(message); },
      onChatRejected: (reason) => { chatRef.current?.onChatRejected(reason); },
    });
    gameRef.current = game;
    game.start();
    return () => {
      game.stop();
      gameRef.current = null;
    };
  }, []);

  // Figur steht, Spiel läuft weiter (AK-4, AK-4b).
  useEffect(() => { gameRef.current?.setMenuOpen(menuShown); }, [menuShown]);

  useEffect(() => {
    if (!menuAvailable) return;
    const abort = new AbortController();
    window.addEventListener(
      "keydown",
      (e) => { if (menuKeyAction(e, false) === "toggle") setMenuOpen((open) => !open); },
      { signal: abort.signal },
    );
    return () => { abort.abort(); };
  }, [menuAvailable]);

  useEffect(() => {
    if (status !== "disconnected") return;
    const watch = new ServerWatch(currentEntryScript(), () => { setUpdate(true); }, browserWatchDeps());
    watch.start();
    return () => { watch.stop(); };
  }, [status]);

  const onReload = () => {
    setReload("pending");
    reloadPage(browserReloadWindow(), () => { setReload("stuck"); });
  };

  return (
    <div className="fixed inset-0 bg-black">
      <canvas ref={canvasRef} className="block h-full w-full touch-none [image-rendering:pixelated]" data-testid="game" />
      <div className="pointer-events-none absolute inset-0 touch-none font-mono text-sm uppercase" data-testid="hud" inert={menuShown}>
        <div className="absolute left-[max(0.5rem,env(safe-area-inset-left))] right-[max(0.5rem,env(safe-area-inset-right))] top-[max(0.5rem,env(safe-area-inset-top))] flex flex-wrap items-start gap-2">
          {name && <span className="badge badge-neutral rounded-none border-2 border-base-content/30">{name}</span>}
          {status !== "kicked" && (
            <span
              className={`badge rounded-none border-2 border-base-content/30 ${status === "connected" ? "badge-success" : status === "connecting" ? "badge-warning" : "badge-error"}`}
            >
              {STATUS_LABEL[status]}
            </span>
          )}
          {coordinates && (
            <span className="badge badge-neutral rounded-none border-2 border-base-content/30" data-testid="coords">{coordinates}</span>
          )}
          <button type="button" className="btn min-h-11 rounded-none font-mono uppercase pointer-events-auto ml-auto" data-testid="menu-button" onClick={() => { setMenuOpen(true); }}>{t.menu}</button>
          {vitals && <Vitals vitals={vitals} />}
        </div>
        <AttackButtons hasTarget={target !== null} onAttack={attack} />
        <Chat ref={chatRef} sendChat={sendChat} inGame={status === "connected" && !menuShown} />
        <AttackHint ref={hintRef} />
        {status === "disconnected" && (
          <div className={OVERLAY}>
            {update && <p role="status" className="max-w-xs normal-case">{t.updateAvailable}</p>}
            <button type="button" disabled={reload === "pending"} data-testid="reload" className="btn btn-primary btn-lg rounded-none font-mono uppercase" onClick={onReload}>
              {reload === "pending" ? t.reloading : update ? t.updateReload : t.reconnect}
            </button>
            {reload === "stuck" && <p role="status" className="max-w-xs normal-case">{t.reloadStuck}</p>}
          </div>
        )}
        {status === "kicked" && kicked === Kicked_Reason.SIGNED_IN_ELSEWHERE && (
          <div className={OVERLAY} data-testid="kicked">
            <p role="status" className="max-w-xs normal-case">{t.kickedSignedInElsewhere}</p>
            <button type="button" disabled={reload === "pending"} data-testid="continue-here" className="btn btn-primary btn-lg rounded-none font-mono uppercase" onClick={onReload}>
              {reload === "pending" ? t.reloading : t.kickedContinueHere}
            </button>
            {reload === "stuck" && <p role="status" className="max-w-xs normal-case">{t.reloadStuck}</p>}
          </div>
        )}
        {status === "kicked" && kicked === Kicked_Reason.SIGNED_OUT && (
          <div className={OVERLAY} data-testid="signed-out">
            <p role="status" className="max-w-xs normal-case">{t.reloading}</p>
          </div>
        )}
        <span className="absolute bottom-[max(0.5rem,env(safe-area-inset-bottom))] right-[max(0.5rem,env(safe-area-inset-right))] text-xs normal-case text-base-content/60" data-testid="version">{APP_VERSION}</span>
      </div>
      {menuShown && <Menu onClose={() => { setMenuOpen(false); }} keyBindings={<KeyBindings bindings={keyBindings} onSave={saveKeyBindings} />} />}
    </div>
  );
}
