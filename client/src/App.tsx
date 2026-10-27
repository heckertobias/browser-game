import { useEffect, useRef, useState } from "react";
import { Game, type ConnectionStatus } from "./game/game";
import { t } from "./i18n";
import { APP_VERSION } from "./version";

const STATUS_LABEL: Record<ConnectionStatus, string> = {
  connecting: t.statusConnecting,
  connected: t.statusConnected,
  disconnected: t.statusDisconnected,
};

export function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [name, setName] = useState<string | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const game = new Game(canvas, { onStatus: setStatus, onWelcome: setName });
    game.start();
    return () => { game.stop(); };
  }, []);

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
          <div className="pointer-events-auto absolute inset-0 flex items-center justify-center bg-black/60 pb-[max(0.5rem,env(safe-area-inset-bottom))] pl-[max(0.5rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))] pt-[max(0.5rem,env(safe-area-inset-top))]">
            <button type="button" className="btn btn-primary btn-lg rounded-none font-mono uppercase" onClick={() => { window.location.reload(); }}>
              {t.reconnect}
            </button>
          </div>
        )}
        <span className="absolute bottom-[max(0.5rem,env(safe-area-inset-bottom))] right-[max(0.5rem,env(safe-area-inset-right))] text-xs normal-case text-base-content/60" data-testid="version">{APP_VERSION}</span>
      </div>
    </div>
  );
}
