import { memo, useEffect, useImperativeHandle, useState, type Ref } from "react";
import { Attack_Kind } from "@/generated/browsergame/v1/game_pb";
import { CooldownSweep, sweepBackground } from "@/features/game/combat/cooldownSweep";
import { t } from "@/features/localization/i18n";

// Unten rechts über der Versionsanzeige (plan.md Entscheidung 9); +1.25rem wie beim Chat über der Version.
const ROOT =
  "absolute bottom-[calc(max(0.5rem,env(safe-area-inset-bottom))+1.25rem)] right-[max(0.5rem,env(safe-area-inset-right))] flex flex-col items-end gap-1";
// Über dem ganzen Button, fängt keine Eingaben ab; der Button bleibt klickbar (Server entscheidet, AK-22).
const SWEEP = "pointer-events-none absolute inset-0";
const BUTTON = "btn relative min-h-14 min-w-14 rounded-none font-mono uppercase";

export interface AttackButtonsViewProps {
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
  /** Noch laufender Anteil der Abklingzeit, 1 … 0 (013 AK-22); 0 oder fehlend = kein Sweep. */
  cooldown?: number;
}

interface AttackButtonProps {
  kind: Attack_Kind;
  label: string;
  /** Präfix der Test-IDs: `<testId>` am Button, `<testId>-cooldown` am Sweep. */
  testId: string;
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
  cooldown: number;
}

/**
 * Ein Angriffs-Button. Bewusst eine Funktion statt einer Komponente: So enthält der Baum von `AttackButtonsView` direkt
 * die `<button>`-Elemente, und Tests können ihn ohne Rendern durchsuchen.
 */
function attackButton({ kind, label, testId, hasTarget, onAttack, cooldown }: AttackButtonProps) {
  return (
    // mousedown ohne Fokus: Leertaste oder Enter lösten sonst über den Button erneut aus.
    <button
      type="button"
      data-testid={testId}
      className={BUTTON}
      disabled={!hasTarget}
      onMouseDown={(e) => { e.preventDefault(); }}
      onClick={() => { onAttack(kind); }}
    >
      {label}
      {cooldown > 0 && (
        <span aria-hidden="true" data-testid={`${testId}-cooldown`} className={SWEEP} style={{ background: sweepBackground(cooldown) }} />
      )}
    </button>
  );
}

/** Darstellung ohne Zustand (testbar): beide Buttons, gesperrt ohne Ziel, mit Sweep während der Abklingzeit. */
export function AttackButtonsView({ hasTarget, onAttack, cooldown = 0 }: AttackButtonsViewProps) {
  const shared = { hasTarget, onAttack, cooldown };
  return (
    <div className={ROOT} data-testid="attack-buttons">
      {/* Gesperrte Buttons lassen den Zeiger durch (daisyUI: pointer-events none); die Leiste fängt ihn, sonst liefe die Figur los. */}
      <div className="pointer-events-auto flex gap-2" data-testid="attack-bar">
        {attackButton({ kind: Attack_Kind.MELEE, label: t.attackMelee, testId: "attack-melee", ...shared })}
        {attackButton({ kind: Attack_Kind.RANGED, label: t.attackRanged, testId: "attack-ranged", ...shared })}
      </div>
    </div>
  );
}

/** Startet die Cooldown-Anzeige nach der Bestätigung des Servers (013 AK-22). */
export interface CooldownFeed {
  start(durationMs: number): void;
}

export interface AttackButtonsProps {
  hasTarget: boolean;
  onAttack: (kind: Attack_Kind) => void;
  ref?: Ref<CooldownFeed>;
}

/**
 * Nah- und Fernkampf im HUD für Touch und Maus; ohne Ziel gesperrt (013 AK-2, plan.md Entscheidung 9). Nach einem vom
 * Server bestätigten Angriff läuft auf beiden ein Sweep ab; die Buttons bleiben dabei klickbar, weil allein der Server
 * den Cooldown durchsetzt (AK-22). Hinweise zeigt `AttackHint` (AK-20).
 */
export const AttackButtons = memo(function AttackButtons({ hasTarget, onAttack, ref }: AttackButtonsProps) {
  const [cooldown, setCooldown] = useState(0);
  const [sweep] = useState(() => new CooldownSweep(setCooldown));

  useImperativeHandle(ref, () => ({ start(durationMs) { sweep.start(durationMs); } }), [sweep]);
  useEffect(() => () => { sweep.clear(); }, [sweep]);

  return <AttackButtonsView hasTarget={hasTarget} onAttack={onAttack} cooldown={cooldown} />;
});
