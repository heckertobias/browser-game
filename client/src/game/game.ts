import type { Snapshot, Welcome } from "../generated/browsergame/v1/game_pb";
import { INTERPOLATION_DELAY_MS, LOGICAL_HEIGHT, LOGICAL_WIDTH } from "./constants";
import { InputTracker } from "./input";
import { SnapshotBuffer } from "./interpolation";
import { connect, gameSocketUrl, type GameConnection } from "./net";
import { Predictor } from "./prediction";
import { cameraOrigin, render, type DrawablePlayer } from "./render";
import type { Vec } from "./vector";
import { computeViewport, type Viewport } from "./viewport";

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
  private self: DrawablePlayer | null = null;

  constructor(private readonly canvas: HTMLCanvasElement, private readonly events: GameEvents) {
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("Canvas 2D wird nicht unterstützt");
    this.ctx = ctx;
    this.input = new InputTracker(canvas);
  }

  start(): void {
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
    this.connection?.close();
    this.input.dispose();
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
    const direction = this.input.direction(position, (clientX, clientY, rect) => this.toWorld(clientX, clientY, rect, position));
    this.connection.sendInput(this.predictor.step(direction));
  }

  /** Bildschirm- → Weltkoordinaten; die Kamera ist auf die eigene Figur zentriert. */
  private toWorld(clientX: number, clientY: number, rect: DOMRect, camera: Vec): Vec {
    const origin = cameraOrigin(camera, this.viewport?.scale ?? 1);
    return {
      x: origin.x + ((clientX - rect.left) / rect.width) * LOGICAL_WIDTH,
      y: origin.y + ((clientY - rect.top) / rect.height) * LOGICAL_HEIGHT,
    };
  }

  private draw(now: number): void {
    this.resizeIfNeeded();
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

  private resizeIfNeeded(): void {
    const dpr = window.devicePixelRatio || 1;
    const next = computeViewport(window.innerWidth, window.innerHeight, dpr);
    if (this.viewport?.width === next.width && this.viewport.height === next.height && this.viewport.scale === next.scale) return;
    this.viewport = next;
    this.canvas.width = next.width;
    this.canvas.height = next.height;
    this.canvas.style.width = `${String(next.width / dpr)}px`;
    this.canvas.style.height = `${String(next.height / dpr)}px`;
  }
}
