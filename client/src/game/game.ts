import type { Snapshot, Welcome } from "../generated/browsergame/v1/game_pb";
import { t } from "../i18n";
import { INTERPOLATION_DELAY_MS } from "./constants";
import { InputTracker } from "./input";
import { SnapshotBuffer } from "./interpolation";
import { connect, gameSocketUrl, type GameConnection } from "./net";
import { Predictor } from "./prediction";
import { cameraOrigin, render, screenToWorld, type DrawablePlayer } from "./render";
import type { Vec } from "./vector";
import { fitViewport, type Viewport } from "./viewport";
import { ScreenWakeLock } from "./wakeLock";

export type ConnectionStatus = "connecting" | "connected" | "disconnected";

export interface GameEvents {
  onStatus(status: ConnectionStatus): void;
  onWelcome(name: string): void;
}

/** Nach längerer Pause (Tab im Hintergrund) höchstens so viel Zeit nachholen. */
const MAX_FRAME_MS = 250;

/** Verbindet Netzwerk, Eingabe, Prediction und Rendering in einer festen 50-ms-Schrittfolge wie der Server. */
export class Game {
  private readonly ctx: CanvasRenderingContext2D;
  private readonly input: InputTracker;
  private readonly snapshots = new SnapshotBuffer();
  private connection: GameConnection | null = null;
  private welcome: Welcome | null = null;
  private predictor: Predictor | null = null;
  private stepMs = 50;
  private accumulator = 0;
  private lastFrame = 0;
  private frameHandle = 0;
  private viewport: Viewport | null = null;
  private pendingSize: { width: number; height: number } | null = null;
  private exactSize = false;
  private dpr = 0;
  private readonly resizeObserver = new ResizeObserver((entries) => { this.handleResize(entries); });
  private readonly wakeLock = new ScreenWakeLock(navigator.wakeLock, document);
  private self: DrawablePlayer | null = null;

  constructor(private readonly canvas: HTMLCanvasElement, private readonly events: GameEvents) {
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error(t.canvasUnsupported);
    this.ctx = ctx;
    this.input = new InputTracker(canvas);
  }

  start(): void {
    this.dpr = window.devicePixelRatio || 1;
    this.observeSize();
    this.wakeLock.start();
    this.events.onStatus("connecting");
    this.connection = connect(gameSocketUrl(window.location), {
      onWelcome: (welcome) => { this.handleWelcome(welcome); },
      onSnapshot: (snapshot) => { this.handleSnapshot(snapshot); },
      onClose: () => { this.events.onStatus("disconnected"); },
    });
    this.lastFrame = performance.now();
    this.frameHandle = requestAnimationFrame(this.frame);
  }

  stop(): void {
    cancelAnimationFrame(this.frameHandle);
    this.resizeObserver.disconnect();
    this.connection?.close();
    this.input.dispose();
    this.wakeLock.stop();
  }

  private handleWelcome(welcome: Welcome): void {
    this.welcome = welcome;
    this.stepMs = 1000 / welcome.tickRateHz;
    this.predictor = new Predictor(welcome.speed / welcome.tickRateHz);
    this.events.onWelcome(welcome.name);
    this.events.onStatus("connected");
  }

  private handleSnapshot(snapshot: Snapshot): void {
    const selfId = this.welcome?.playerId;
    const own = snapshot.players.find((p) => p.id === selfId);
    if (own && this.predictor) {
      this.predictor.reconcile(own.x, own.y, snapshot.lastProcessedSeq);
      this.self = { name: own.name, color: own.color, x: own.x, y: own.y };
    }
    this.snapshots.push(performance.now(), snapshot.players.filter((p) => p.id !== selfId));
  }

  private readonly frame = (now: number): void => {
    this.accumulator += Math.min(now - this.lastFrame, MAX_FRAME_MS);
    this.lastFrame = now;

    while (this.accumulator >= this.stepMs) {
      this.fixedStep();
      this.accumulator -= this.stepMs;
    }

    this.draw(now);
    this.frameHandle = requestAnimationFrame(this.frame);
  };

  private fixedStep(): void {
    if (!this.predictor?.initialized || !this.connection) return;
    const position = { x: this.predictor.x, y: this.predictor.y };
    const direction = this.input.direction(position, (clientX, clientY, rect) => {
      if (!this.viewport || rect.width <= 0 || rect.height <= 0) return position;
      return screenToWorld(
        clientX - rect.left,
        clientY - rect.top,
        rect.width,
        rect.height,
        this.viewport,
        cameraOrigin(position, this.viewport),
      );
    });
    this.connection.sendInput(this.predictor.step(direction));
  }

  private draw(now: number): void {
    this.updateViewport();
    if (!this.viewport) return;

    const others = this.snapshots.sample(now - INTERPOLATION_DELAY_MS);
    const players: DrawablePlayer[] = [...others];
    let camera: Vec = { x: 0, y: 0 };
    if (this.predictor?.initialized && this.self) {
      camera = this.predictor.renderPosition(this.accumulator / this.stepMs);
      players.push({ ...this.self, ...camera });
    }
    render(this.ctx, this.viewport, camera, players);
  }

  private observeSize(): void {
    try {
      this.resizeObserver.observe(this.canvas, { box: "device-pixel-content-box" });
    } catch {
      this.resizeObserver.observe(this.canvas); // ältere Browser kennen die Box nicht
    }
  }

  private handleResize(entries: readonly ResizeObserverEntry[]): void {
    for (const entry of entries) {
      const exact = "devicePixelContentBoxSize" in entry ? entry.devicePixelContentBoxSize[0] : undefined;
      this.exactSize = exact !== undefined;
      this.pendingSize = exact ? { width: exact.inlineSize, height: exact.blockSize } : this.measureCss();
    }
  }

  /** Fallback ohne devicePixelContentBoxSize (Safari): iOS hat ganzzahlige DPR, dort exakt. */
  private measureCss(): { width: number; height: number } {
    const dpr = window.devicePixelRatio || 1;
    return { width: Math.round(this.canvas.clientWidth * dpr), height: Math.round(this.canvas.clientHeight * dpr) };
  }

  /** Übernimmt eine gemeldete Größe im Frame (AK-5); Größe 0 (minimiert) wird nicht übernommen. */
  private updateViewport(): void {
    const dpr = window.devicePixelRatio || 1;
    if (dpr !== this.dpr) {
      this.dpr = dpr;
      // Mit device-pixel-content-box meldet der ResizeObserver DPR-Wechsel selbst; im Fallback ändert sich die CSS-Größe nicht.
      if (!this.exactSize) this.pendingSize = this.measureCss();
    }
    const size = this.pendingSize;
    if (!size) return;
    this.pendingSize = null;
    if (size.width <= 0 || size.height <= 0) {
      this.viewport = null;
      return;
    }
    if (this.viewport?.canvasWidth === size.width && this.viewport.canvasHeight === size.height) return;
    this.viewport = fitViewport(size.width, size.height);
    this.canvas.width = this.viewport.canvasWidth;
    this.canvas.height = this.viewport.canvasHeight;
  }
}
