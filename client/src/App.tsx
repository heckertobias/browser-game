import { useEffect, useRef, useState } from "react";
import { Game, type ConnectionStatus } from "./game/game";
import { t } from "./i18n";
import { browserReloadWindow, reloadPage } from "./reload";
import { ServerWatch, type ServerWatchDeps } from "./update";
import { APP_VERSION } from "./version";

const STATUS_LABEL: Record<ConnectionStatus, string> = {
  connecting: t.statusConnecting,
  connected: t.statusConnected,
  disconnected: t.statusDisconnected,
};

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
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [name, setName] = useState<string | null>(null);
  const [update, setUpdate] = useState(false);
  const [reload, setReload] = useState<"idle" | "pending" | "stuck">("idle");

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const game = new Game(canvas, { onStatus: setStatus, onWelcome: setName });
    game.start();
    return () => { game.stop(); };
  }, []);

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
      <div className="pointer-events-none absolute inset-0 font-mono text-sm uppercase">
        <div className="absolute left-[max(0.5rem,env(safe-area-inset-left))] right-[max(0.5rem,env(safe-area-inset-right))] top-[max(0.5rem,env(safe-area-inset-top))] flex flex-wrap gap-2">
          {name && <span className="badge badge-neutral rounded-none border-2 border-base-content/30">{name}</span>}
          <span
            className={`badge rounded-none border-2 border-base-content/30 ${status === "connected" ? "badge-success" : status === "connecting" ? "badge-warning" : "badge-error"}`}
          >
            {STATUS_LABEL[status]}
          </span>
        </div>
        {status === "disconnected" && (
          <div className="pointer-events-auto absolute inset-0 flex flex-col items-center justify-center gap-4 bg-black/60 text-center pb-[max(0.5rem,env(safe-area-inset-bottom))] pl-[max(0.5rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))] pt-[max(0.5rem,env(safe-area-inset-top))]">
            {update && <p role="status" className="max-w-xs normal-case">{t.updateAvailable}</p>}
            <button type="button" disabled={reload === "pending"} data-testid="reload" className="btn btn-primary btn-lg rounded-none font-mono uppercase" onClick={onReload}>
              {reload === "pending" ? t.reloading : update ? t.updateReload : t.reconnect}
            </button>
            {reload === "stuck" && <p role="status" className="max-w-xs normal-case">{t.reloadStuck}</p>}
          </div>
        )}
        <span className="absolute bottom-[max(0.5rem,env(safe-area-inset-bottom))] right-[max(0.5rem,env(safe-area-inset-right))] text-xs normal-case text-base-content/60" data-testid="version">{APP_VERSION}</span>
      </div>
    </div>
  );
}
