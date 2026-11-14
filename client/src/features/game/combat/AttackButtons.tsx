import { memo } from "react";
import { Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { t } from "@/features/localization/i18n";

// Unten rechts über der Versionsanzeige (plan.md Entscheidung 9); +1.25rem wie beim Chat über der Version.
const ROOT =
  "absolute bottom-[calc(max(0.5rem,env(safe-area-inset-bottom))+1.25rem)] right-[max(0.5rem,env(safe-area-inset-right))] flex flex-col items-end gap-1";
const BUTTON = "btn min-h-14 min-w-14 rounded-none font-mono uppercase";

export interface AttackButtonsViewProps {
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
}

export function AttackButtonsView({ hasTarget, onAttack }: AttackButtonsViewProps) {
  return (
    <div className={ROOT} data-testid="attack-buttons">
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

export interface AttackButtonsProps {
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
}

/** Nah- und Fernkampf im HUD; ohne Ziel gesperrt (plan.md Entscheidung 9). Hinweise zeigt `AttackHint` (AK-20). */
export const AttackButtons = memo(function AttackButtons({ hasTarget, onAttack }: AttackButtonsProps) {
  return <AttackButtonsView hasTarget={hasTarget} onAttack={onAttack} />;
});
