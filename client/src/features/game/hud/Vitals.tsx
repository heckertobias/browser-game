import type { PlayerVitals } from "@/features/game/engine/game";
import { t } from "@/features/localization/i18n";

// [--radius-box:0px]: eckig wie die Badges; daisyUI rundet den Balken und seinen Füllwert sonst über diese Variable.
const BAR = "progress h-2 w-32 [--radius-box:0px]";

/** Abgerundeter Wert und Maximum, z. B. „73/100“; nie unter 0 (012 AK-5). */
export function vitalsText(value: number, max: number): string {
  return `${String(Math.max(0, Math.floor(value)))}/${String(Math.max(0, Math.floor(max)))}`;
}

/** Füllstand in Prozent, begrenzt auf 0–100; ohne gültiges Maximum 0. */
export function fillPercent(value: number, max: number): number {
  if (!(max > 0)) return 0;
  return Math.min(100, Math.max(0, (value / max) * 100));
}

function Bar({ label, value, max, color, testId }: { label: string; value: number; max: number; color: string; testId: string }) {
  const text = vitalsText(value, max);
  return (
    <div className="flex items-center gap-2">
      <span className="min-w-[10ch] whitespace-nowrap">{`${label} ${text}`}</span>
      <progress
        className={`${BAR} ${color}`}
        value={fillPercent(value, max)}
        max={100}
        aria-label={label}
        aria-valuetext={text}
        data-testid={testId}
      />
    </div>
  );
}

/** HP- und Mana-Balken im HUD, oben links unter Name/Status (012 AK-5, AK-6). */
export function Vitals({ vitals }: { vitals: PlayerVitals }) {
  return (
    <div className="basis-full" data-testid="vitals">
      <div className="flex w-fit flex-col gap-1 bg-black/50 px-2 py-1">
        <Bar label={t.hp} value={vitals.hp} max={vitals.maxHp} color="progress-error" testId="vitals-hp" />
        <Bar label={t.mana} value={vitals.mana} max={vitals.maxMana} color="progress-info" testId="vitals-mana" />
      </div>
    </div>
  );
}
