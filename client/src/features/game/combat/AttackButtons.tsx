import { memo, useEffect, useImperativeHandle, useState, type Ref } from "react";
import { AttackRejected_Reason, Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import type { AttackRejectReason } from "@/features/game/network/net";
import { t } from "@/features/localization/i18n";

/** So lange steht ein Hinweis (AK-8, AK-19). Bedienung, kein Spielwert (Constitution §8). */
export const ATTACK_HINT_MS = 2000;

// Unten rechts über der Versionsanzeige (plan.md Entscheidung 9); +1.25rem wie beim Chat über der Version.
const ROOT =
  "absolute bottom-[calc(max(0.5rem,env(safe-area-inset-bottom))+1.25rem)] right-[max(0.5rem,env(safe-area-inset-right))] flex flex-col items-end gap-1";
const BUTTON = "btn min-h-14 min-w-14 rounded-none font-mono uppercase";

export function hintText(reason: AttackRejectReason): string {
  return reason === AttackRejected_Reason.NOT_ENOUGH_MANA ? t.notEnoughMana : t.notFacing;
}

/** Zeigt einen Grund für `durationMs`; ein neuer ersetzt den alten und startet die Zeit neu. */
export class AttackHint {
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private readonly onChange: (reason: AttackRejectReason | null) => void,
    private readonly durationMs = ATTACK_HINT_MS,
  ) {}

  show(reason: AttackRejectReason): void {
    clearTimeout(this.timer);
    this.onChange(reason);
    this.timer = setTimeout(() => {
      this.timer = undefined;
      this.onChange(null);
    }, this.durationMs);
  }

  /** Beim Abbau: danach kommt kein Rückruf mehr. */
  clear(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
  }
}

export interface AttackButtonsViewProps {
  hasTarget: boolean;
  hint: AttackRejectReason | null;
  onAttack: (kind: Attack_Kind) => void;
}

export function AttackButtonsView({ hasTarget, hint, onAttack }: AttackButtonsViewProps) {
  return (
    <div className={ROOT} data-testid="attack-buttons">
      {hint !== null && (
        <p role="status" data-testid="attack-hint" className="bg-black/50 px-2 py-1 normal-case text-warning">
          {hintText(hint)}
        </p>
      )}
      {/* Gesperrte Buttons lassen den Zeiger durch (daisyUI: pointer-events none); die Leiste fängt ihn, sonst liefe die Figur los. */}
      <div className="pointer-events-auto flex gap-2" data-testid="attack-bar">
        {/* mousedown ohne Fokus: Leertaste oder Enter lösten sonst über den Button erneut aus. */}
        <button
          type="button"
          data-testid="attack-melee"
          className={BUTTON}
          disabled={!hasTarget}
          onMouseDown={(e) => { e.preventDefault(); }}
          onClick={() => { onAttack(Attack_Kind.MELEE); }}
        >
          {t.attackMelee}
        </button>
        <button
          type="button"
          data-testid="attack-ranged"
          className={BUTTON}
          disabled={!hasTarget}
          onMouseDown={(e) => { e.preventDefault(); }}
          onClick={() => { onAttack(Attack_Kind.RANGED); }}
        >
          {t.attackRanged}
        </button>
      </div>
    </div>
  );
}

export interface AttackFeed {
  onAttackRejected(reason: AttackRejectReason): void;
}

export interface AttackButtonsProps {
  ref?: Ref<AttackFeed>;
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
}

/** Nah- und Fernkampf im HUD; ohne Ziel gesperrt (plan.md Entscheidung 9), Ablehnungen kurz als Hinweis (AK-8, AK-19). */
export const AttackButtons = memo(function AttackButtons({ ref, hasTarget, onAttack }: AttackButtonsProps) {
  const [hint, setHint] = useState<AttackRejectReason | null>(null);
  const [timer] = useState(() => new AttackHint(setHint));

  useImperativeHandle(ref, () => ({ onAttackRejected(reason) { timer.show(reason); } }), [timer]);
  useEffect(() => () => { timer.clear(); }, [timer]);

  return <AttackButtonsView hasTarget={hasTarget} hint={hint} onAttack={onAttack} />;
});
