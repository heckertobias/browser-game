import { memo, useEffect, useImperativeHandle, useState, type Ref } from "react";
import { AttackRejected_Reason } from "@/generated/browsergame/v1/game_pb";
import type { AttackRejectReason } from "@/features/game/network/net";
import { t } from "@/features/localization/i18n";

/** So lange steht ein Hinweis (AK-20). Bedienung, kein Spielwert (Constitution §8). */
export const ATTACK_HINT_MS = 2000;

/** Kein Ziel gewählt: erkennt der Client selbst (AK-2). */
export const NO_TARGET = "noTarget";

export type HintReason = AttackRejectReason | typeof NO_TARGET;

const HINT_TEXT: Record<HintReason, string> = {
  [NO_TARGET]: t.noTarget,
  [AttackRejected_Reason.OUT_OF_RANGE]: t.outOfRange,
  [AttackRejected_Reason.NOT_FACING]: t.notFacing,
  [AttackRejected_Reason.NOT_ENOUGH_MANA]: t.notEnoughMana,
};

// Mittig im oberen Drittel des Spielfelds, fängt keine Eingaben ab (AK-20).
const AREA =
  "pointer-events-none absolute inset-x-0 top-1/6 flex justify-center pl-[max(0.5rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))]";
const BOX = "max-w-full border-2 border-warning bg-black/80 px-4 py-2 text-center text-2xl normal-case text-warning";

export function hintText(reason: HintReason): string {
  return HINT_TEXT[reason];
}

/** Zeigt einen Grund für `durationMs`; ein neuer ersetzt den alten und startet die Zeit neu. */
export class HintTimer {
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private readonly onChange: (reason: HintReason | null) => void,
    private readonly durationMs = ATTACK_HINT_MS,
  ) {}

  show(reason: HintReason): void {
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

export function AttackHintView({ reason }: { reason: HintReason | null }) {
  return (
    <div className={AREA} data-testid="attack-hint-area">
      {reason !== null && (
        <p role="status" data-testid="attack-hint" className={BOX}>
          {hintText(reason)}
        </p>
      )}
    </div>
  );
}

export interface HintFeed {
  show(reason: HintReason): void;
}

/** Warum ein Angriff nicht ausgeführt wurde, groß und kurz (AK-20). */
export const AttackHint = memo(function AttackHint({ ref }: { ref?: Ref<HintFeed> }) {
  const [reason, setReason] = useState<HintReason | null>(null);
  const [timer] = useState(() => new HintTimer(setReason));

  useImperativeHandle(ref, () => ({ show(next) { timer.show(next); } }), [timer]);
  useEffect(() => () => { timer.clear(); }, [timer]);

  return <AttackHintView reason={reason} />;
});
