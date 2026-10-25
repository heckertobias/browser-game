import { useEffect, useRef, useState } from "react";
import { Game, type ConnectionStatus } from "./game/game";
import { t } from "./i18n";

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
    <div className="relative flex h-full w-full select-none items-center justify-center bg-black">
      <canvas ref={canvasRef} className="touch-none [image-rendering:pixelated]" data-testid="game" />
      <div className="absolute left-2 top-2 flex gap-2 font-mono text-xs uppercase">
        {name && <span className="badge badge-neutral rounded-none border-2 border-base-content/30">{name}</span>}
        <span
          className={`badge rounded-none border-2 border-base-content/30 ${status === "connected" ? "badge-success" : status === "connecting" ? "badge-warning" : "badge-error"}`}
        >
          {STATUS_LABEL[status]}
        </span>
      </div>
      {status === "disconnected" && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/60">
          <button type="button" className="btn btn-primary rounded-none font-mono uppercase" onClick={() => { window.location.reload(); }}>
            {t.reconnect}
          </button>
        </div>
      )}
    </div>
  );
}
