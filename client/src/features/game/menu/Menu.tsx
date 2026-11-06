import { useEffect, useRef, useState, type RefObject } from "react";
import { t } from "@/features/localization/i18n";
import type { MenuPage } from "./menuLogic";

// Liegt neben dem HUD (das ist bei offenem Menü inert), daher eigene Schrift- und Gestenklassen.
// my-auto im Inhalt statt justify-center: Wird der Inhalt höher als der Schirm (Handy quer), bleibt er scrollbar.
const OVERLAY =
  "absolute inset-0 flex flex-col items-center overflow-y-auto overscroll-contain touch-pan-y bg-black/60 text-center font-mono text-sm uppercase pb-[max(0.5rem,env(safe-area-inset-bottom))] pl-[max(0.5rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))] pt-[max(0.5rem,env(safe-area-inset-top))]";
const ENTRY = "btn btn-lg min-h-11 w-full rounded-none font-mono uppercase";

export interface MenuViewProps {
  page: MenuPage;
  /** Erstes Element der Seite; bekommt beim Anzeigen den Fokus. */
  focusRef: RefObject<HTMLButtonElement | null>;
  onContinue: () => void;
  onShowKeyBindings: () => void;
  onBack: () => void;
}

export function MenuView({ page, focusRef, onContinue, onShowKeyBindings, onBack }: MenuViewProps) {
  return (
    <div className={OVERLAY} role="dialog" aria-modal="true" aria-label={t.menu} data-testid="menu">
      {page === "main" ? (
        <div className="my-auto flex w-full max-w-xs flex-col gap-4" data-testid="menu-main">
          <h2 className="text-lg">{t.menu}</h2>
          <button ref={focusRef} type="button" data-testid="menu-continue" className={`${ENTRY} btn-primary`} onClick={onContinue}>{t.menuContinue}</button>
          <button type="button" data-testid="menu-key-bindings" className={ENTRY} onClick={onShowKeyBindings}>{t.menuKeyBindings}</button>
          <a href="/Account/Manage" draggable={false} data-testid="menu-account" className={ENTRY}>{t.myAccount}</a>
          <a href="/Account/Logout" draggable={false} data-testid="menu-sign-out" className={ENTRY}>{t.menuSignOut}</a>
        </div>
      ) : (
        <div className="my-auto flex w-full max-w-xs flex-col gap-4" data-testid="menu-key-bindings-page">
          <h2 className="text-lg">{t.menuKeyBindings}</h2>
          <button ref={focusRef} type="button" data-testid="menu-back" className={ENTRY} onClick={onBack}>{t.back}</button>
        </div>
      )}
    </div>
  );
}

export function Menu({ onClose }: { onClose: () => void }) {
  const [page, setPage] = useState<MenuPage>("main");
  const focusRef = useRef<HTMLButtonElement>(null);
  useEffect(() => { focusRef.current?.focus(); }, [page]);
  return (
    <MenuView
      page={page}
      focusRef={focusRef}
      onContinue={onClose}
      onShowKeyBindings={() => { setPage("keyBindings"); }}
      onBack={() => { setPage("main"); }}
    />
  );
}
