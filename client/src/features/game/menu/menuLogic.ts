import { isEditableTarget } from "@/features/game/input/input";

export type MenuPage = "main" | "keyBindings";

export type MenuKeyAction = "toggle" | "cancelRecording" | null;

/**
 * Esc öffnet und schließt das Menü (AK-1); beim Aufnehmen einer Taste bricht es nur das Aufnehmen ab.
 * Nicht in Eingabefeldern und nicht bei `repeat`: Ein im Chat gehaltenes Esc käme sonst außerhalb des Felds an.
 */
export function menuKeyAction(e: Pick<KeyboardEvent, "key" | "repeat" | "target">, recording: boolean): MenuKeyAction {
  if (e.key !== "Escape" || e.repeat || isEditableTarget(e.target)) return null;
  return recording ? "cancelRecording" : "toggle";
}
