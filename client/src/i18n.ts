const LANGUAGES = ["en", "de"] as const;

export type Language = (typeof LANGUAGES)[number];

const en = {
  statusConnecting: "connecting …",
  statusConnected: "connected",
  statusDisconnected: "disconnected",
  reconnect: "Reconnect",
  canvasUnsupported: "Canvas 2D is not supported",
  kickedSignedInElsewhere: "Signed in on another device",
  kickedContinueHere: "Continue here",
  offlineTitle: "No connection",
  offlineMessage: "Check your internet connection.",
  offlineRetry: "Try again",
} as const;

type Texts = Record<keyof typeof en, string>;

// Direktes Literal, kein `...en`: sonst füllt der Spread fehlende Schlüssel still mit Englisch.
const de: Texts = {
  statusConnecting: "verbinde …",
  statusConnected: "verbunden",
  statusDisconnected: "getrennt",
  reconnect: "Neu verbinden",
  canvasUnsupported: "Canvas 2D wird nicht unterstützt",
  kickedSignedInElsewhere: "Auf einem anderen Gerät angemeldet",
  kickedContinueHere: "Hier weiterspielen",
  offlineTitle: "Keine Verbindung",
  offlineMessage: "Prüfe deine Internetverbindung.",
  offlineRetry: "Erneut versuchen",
};

/** Beide Wörterbücher, z. B. für Tests. */
export const dictionaries: Record<Language, Texts> = { en, de };

const CULTURE_COOKIE = ".AspNetCore.Culture";

function normalize(code: string): Language | undefined {
  const base = code.trim().toLowerCase().split("-")[0];
  return LANGUAGES.find((language) => language === base);
}

function cookieLanguage(cookie: string): Language | undefined {
  const entry = cookie.split(";").map((part) => part.trim()).find((part) => part.startsWith(`${CULTURE_COOKIE}=`));
  if (!entry) return undefined;
  let value: string;
  try {
    value = decodeURIComponent(entry.slice(CULTURE_COOKIE.length + 1));
  } catch {
    return undefined;
  }
  const parts = value.split("|");
  const find = (name: string) => parts.find((part) => part.startsWith(`${name}=`))?.slice(name.length + 1);
  const uic = find("uic");
  const c = find("c");
  return (uic === undefined ? undefined : normalize(uic)) ?? (c === undefined ? undefined : normalize(c));
}

/** Sprache des Culture-Cookies (ASP.NET Core), sonst die des Browsers, sonst Englisch. */
export function detectLanguage(cookie: string, browserLanguage: string): Language {
  return cookieLanguage(cookie) ?? normalize(browserLanguage) ?? "en";
}

// Zur Laufzeit nicht umschaltbar: im Spiel gibt es keinen Umschalter (006 AK-8b).
export const language: Language = detectLanguage(
  typeof document !== "undefined" ? document.cookie : "",
  typeof navigator !== "undefined" ? navigator.language : "",
);

export const t: Texts = language === "de" ? de : en;
