import type { Attack_Kind, Snapshot, TargetState, Welcome } from "@/generated/browsergame/v1/game_pb";
import { t } from "@/features/localization/i18n";
import { INTERPOLATION_DELAY_MS, TARGET_HIT_RADIUS } from "@/features/game/core/constants";
import { attackRequest, pickTarget, TargetSelection } from "@/features/game/combat/targeting";
import { fromProto, toProto, type Bindings } from "@/features/game/input/bindings";
import { InputTracker } from "@/features/game/input/input";
import { formatCoordinates } from "@/features/game/render/hud";
import { SnapshotBuffer } from "@/features/game/state/interpolation";
import { connect, gameSocketUrl, type AttackRejectReason, type ChatHandlers, type GameConnection, type KickReason } from "@/features/game/network/net";
import { Predictor } from "@/features/game/state/prediction";
import { cameraOrigin, render, screenToWorld, type DrawablePlayer } from "@/features/game/render/render";
import type { Vec } from "@/features/game/core/vector";
import { fitViewport, type Viewport } from "@/features/game/render/viewport";
import { ScreenWakeLock } from "@/features/game/input/wakeLock";

export type ConnectionStatus = "connecting" | "connected" | "disconnected" | "kicked";

/** Eigene HP und Mana mit Maximum, abgerundet (012 AK-5). */
export interface PlayerVitals {
  hp: number;
  maxHp: number;
  mana: number;
  maxMana: number;
}

export interface GameEvents extends ChatHandlers {
  onStatus(status: ConnectionStatus): void;
  onWelcome(name: string): void;
  onKicked(reason: KickReason): void;
  /** Eigene Koordinaten (gerundet, aus der vorhergesagten Position), nur bei Änderung (AK-19b). */
  onCoordinates(text: string): void;
  /** Eigene HP/Mana (abgerundet), nur bei Änderung (012 AK-6). Ab dem ersten Snapshot mit eigener Figur. */
  onVitals?(vitals: PlayerVitals): void;
  /** Aktuelle Belegung: nach Welcome (gespeicherte des Accounts, AK-9) und nach `saveKeyBindings`. */
  onKeyBindings?(bindings: Bindings): void;
  /** Aktives Ziel nach Antippen/Anklicken oder `null`, wenn es im Snapshot fehlt (013 AK-1, plan.md Entscheidung 2). Nur bei Änderung. */
  onTarget?(id: string | null): void;
  /** Angriff abgelehnt: zu wenig Mana (AK-8) oder Ziel nicht vor dir (AK-19). */
  onAttackRejected?(reason: AttackRejectReason): void;
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
  private coordinates = "";
  private vitals: PlayerVitals | null = null;
  private readonly projectiles = new SnapshotBuffer<Vec & { id: number }>();
  private readonly selection = new TargetSelection();
  private targets: readonly TargetState[] = [];
  /** Zuletzt gezeichnete Kamera; Tippen auf ein Ziel rechnet mit dem, was der Spieler sieht. */
  private camera: Vec = { x: 0, y: 0 };

  constructor(private readonly canvas: HTMLCanvasElement, private readonly events: GameEvents) {
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error(t.canvasUnsupported);
    this.ctx = ctx;
    this.input = new InputTracker(canvas, {
      pickTarget: (clientX, clientY) => this.pickTargetAt(clientX, clientY),
      onAttack: (kind) => { this.attack(kind); },
    });
  }

  start(): void {
    this.dpr = window.devicePixelRatio || 1;
    this.observeSize();
    this.wakeLock.start();
    this.events.onStatus("connecting");
    this.connection = connect(gameSocketUrl(window.location), {
      onWelcome: (welcome) => { this.handleWelcome(welcome); },
      onSnapshot: (snapshot) => { this.handleSnapshot(snapshot); },
      onKicked: (reason) => {
        this.events.onKicked(reason);
        this.events.onStatus("kicked");
      },
      onClose: () => { this.events.onStatus("disconnected"); },
      onChatHistory: (messages) => { this.events.onChatHistory(messages); },
      onChatMessage: (message) => { this.events.onChatMessage(message); },
      onChatRejected: (reason) => { this.events.onChatRejected(reason); },
      onAttackRejected: (reason) => { this.events.onAttackRejected?.(reason); },
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

  /** `false`: nichts gesendet (Verbindung nicht offen), der Text bleibt beim Aufrufer. */
  sendChat(text: string): boolean {
    return this.connection?.sendChat(text) ?? false;
  }

  /** Sofort lokal wirksam (AK-5) und an den Server; bei nicht offener Verbindung geht nur das Senden verloren. */
  saveKeyBindings(bindings: Bindings): void {
    this.applyKeyBindings(bindings);
    this.connection?.saveKeyBindings(toProto(bindings));
  }

  /** Angriff auf das aktive Ziel (Tasten und Buttons). Ohne Ziel wird nichts geschickt (AK-2). */
  attack(kind: Attack_Kind): void {
    const request = attackRequest(this.selection, kind);
    if (request !== null) this.connection?.sendAttack(request);
  }

  /** Menü offen: Figur steht (AK-4); Snapshots und Rendern laufen weiter (AK-4b). */
  setMenuOpen(open: boolean): void {
    this.input.setSuspended(open);
  }

  private applyKeyBindings(bindings: Bindings): void {
    this.input.setBindings(bindings);
    this.events.onKeyBindings?.(bindings);
  }

  private handleWelcome(welcome: Welcome): void {
    this.welcome = welcome;
    this.stepMs = 1000 / welcome.tickRateHz;
    this.predictor = new Predictor(welcome.speed / welcome.tickRateHz, welcome.minFacingInput);
    this.applyKeyBindings(fromProto(welcome.keyBindings));
    this.events.onWelcome(welcome.name);
    this.events.onStatus("connected");
  }

  private handleSnapshot(snapshot: Snapshot): void {
    const now = performance.now();
    const selfId = this.welcome?.playerId;
    const own = snapshot.players.find((p) => p.id === selfId);
    if (own && this.predictor) {
      this.predictor.reconcile(own.x, own.y, snapshot.lastProcessedSeq, own.facing);
      this.self = { name: own.name, color: own.color, x: own.x, y: own.y };
    }
    if (own) this.reportVitals(snapshot);
    this.snapshots.push(now, snapshot.players.filter((p) => p.id !== selfId));
    this.targets = snapshot.targets;
    const selected = this.selection.current;
    this.selection.sync(snapshot.targets.map((target) => target.id));
    if (this.selection.current !== selected) this.events.onTarget?.(this.selection.current);
    this.projectiles.push(now, snapshot.projectiles.map(({ id, x, y }) => ({ id, x, y })));
  }

  /** Liegt unter dem Zeiger ein Ziel, wird es aktiv (AK-1, AK-3); dann läuft die Figur nicht los (plan.md Entscheidung 7). */
  private pickTargetAt(clientX: number, clientY: number): boolean {
    if (!this.viewport) return false;
    const rect = this.canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return false;
    const world = screenToWorld(
      clientX - rect.left,
      clientY - rect.top,
      rect.width,
      rect.height,
      this.viewport,
      cameraOrigin(this.camera, this.viewport),
    );
    const id = pickTarget(world, this.targets, TARGET_HIT_RADIUS);
    if (id === null) return false;
    if (id !== this.selection.current) {
      this.selection.select(id);
      this.events.onTarget?.(id);
    }
    return true;
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
    this.reportCoordinates();
  }

  private reportCoordinates(): void {
    if (!this.predictor) return;
    const text = formatCoordinates(this.predictor.x, this.predictor.y);
    if (text === this.coordinates) return;
    this.coordinates = text;
    this.events.onCoordinates(text);
  }

  private reportVitals(snapshot: Snapshot): void {
    if (!this.welcome) return;
    const next: PlayerVitals = {
      hp: Math.floor(snapshot.hp),
      maxHp: Math.floor(this.welcome.maxHp),
      mana: Math.floor(snapshot.mana),
      maxMana: Math.floor(this.welcome.maxMana),
    };
    const last = this.vitals;
    if (last?.hp === next.hp && last.maxHp === next.maxHp && last.mana === next.mana && last.maxMana === next.maxMana) return;
    this.vitals = next;
    this.events.onVitals?.(next);
  }

  private draw(now: number): void {
    this.updateViewport();
    if (!this.viewport) return;

    const others = this.snapshots.sample(now - INTERPOLATION_DELAY_MS);
    const players: DrawablePlayer[] = [...others];
    let camera: Vec = { x: 0, y: 0 };
    if (this.predictor?.initialized && this.self) {
      camera = this.predictor.renderPosition(this.accumulator / this.stepMs);
      players.push({ ...this.self, ...camera, facing: this.predictor.facing });
    }
    this.camera = camera;
    const projectiles = this.projectiles.sample(now - INTERPOLATION_DELAY_MS);
    render(this.ctx, this.viewport, camera, players, {
      targets: this.targets,
      projectiles,
      selectedId: this.selection.current,
    });
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
