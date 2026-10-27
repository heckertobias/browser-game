/** Ohne Seitenwechsel nach so vielen ms gilt das Neuladen als hängend (007 AK-16). */
export const RELOAD_STUCK_MS = 8000;

/** Was reloadPage vom Fenster braucht (in Tests gefälscht). */
export interface ReloadWindow {
  readonly location: Pick<Location, "reload">;
  setTimeout(handler: () => void, timeout: number): number;
}

/**
 * Lädt neu. Lebt die Seite nach RELOAD_STUCK_MS noch, ruft sie onStuck (iOS-Homescreen-App: Neuladen kann hängen
 * oder ignoriert werden). Die Rückmeldung „lädt …“ setzt der Aufrufer vorher.
 */
export function reloadPage(win: ReloadWindow, onStuck: () => void): void {
  win.setTimeout(onStuck, RELOAD_STUCK_MS);
  win.location.reload();
}

/** `window` ungebunden zu übergeben löst in Safari „Illegal invocation“ aus. */
export function browserReloadWindow(): ReloadWindow {
  return { location: window.location, setTimeout: (handler, timeout) => window.setTimeout(handler, timeout) };
}
