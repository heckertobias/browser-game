# 026 – Spec-Nummer eindeutig

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Gehört zu: [ADR 0016](../../docs/decisions/0016-specs-von-jedem-branch.md) (neue Specs auf jedem Branch)

## Ziel

Neue Specs dürfen auf jedem Branch entstehen (ADR 0016). Damit können zwei Branches unabhängig voneinander dieselbe
Nummer vergeben. Die CI erkennt das und wird rot, bevor zwei verschiedene Specs mit derselben Nummer auf `main`
liegen. So bleibt jede Nummer eindeutig, und Verweise wie „013 AK-2“ in Code, Tests und Roadmap zeigen immer auf genau
eine Spec.

## User Stories

- Als **Entwickler oder Agent** möchte ich eine neue Spec auf meinem aktuellen Branch anlegen und sofort erfahren, wenn
  ihre Nummer auf `main` schon vergeben ist, damit ich vor dem Merge umnummeriere.
- Als **Betreuer des Repos** möchte ich, dass jede Nummer auf `main` genau eine Spec bezeichnet, damit Verweise
  eindeutig bleiben.

## Akzeptanzkriterien

- **AK-1:** Gegeben auf `main` liegt eine Spec mit der Nummer N, wenn mein Branch eine andere, neue Spec mit derselben
  Nummer N anlegt, dann schlägt die Prüfung in der CI fehl.
- **AK-2:** Gegeben die Prüfung schlägt wegen AK-1 fehl, dann nennt die Meldung beide Specs und die nächste freie
  Nummer.
- **AK-3:** Gegeben mein Branch ändert eine Spec, die es auf `main` schon gibt, dann ist das kein Konflikt.
- **AK-4:** Gegeben mein Branch benennt eine Spec um und behält ihre Nummer, dann ist das kein Konflikt.
- **AK-5:** Gegeben ein Stand enthält zwei verschiedene Specs mit derselben Nummer, auch `main` selbst, dann schlägt
  die Prüfung fehl und nennt beide.
- **AK-6:** Gegeben ich führe die Prüfung lokal aus und habe einen aktuellen Stand von `main`, dann sehe ich dieselben
  Fehler wie in der CI. Ohne Stand von `main` prüft sie nur AK-5 und sagt das.
- **AK-7:** Gegeben ein Branch ändert nur Markdown, dann läuft die Prüfung trotzdem.

## Out of Scope

- Zwei offene Branches mit derselben Nummer, solange keiner gemergt ist: Der zuerst gemergte gewinnt, der andere wird
  bei seinem nächsten Lauf rot (AK-1).
- Automatisch umnummerieren.
- Prüfen, ob `plan.md` und `tasks.md` erstmals auf einem `spec/`-Branch entstanden sind (ADR 0016): eine Regel ohne
  Prüfung.
- Branch-Namen prüfen.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Was „belegt“ heißt:** Auf `main` gibt es einen Spec-Ordner mit dieser Nummer. Derselbe Ordner auf dem Branch ist
  dieselbe Spec (AK-3).
- **Kein Zwang, vor dem Merge auf den neuesten Stand von `main` zu gehen:** Kollisionen sind selten, und der Zwang
  stieße bei jeder Änderung an `main` jeden offenen PR neu an, auch die von Dependabot. Ändert sich `main` nach dem
  letzten Lauf eines Branches, fällt der Konflikt beim nächsten Lauf auf, spätestens auf `main` selbst (AK-5).
- **Teil der bestehenden Doku-Prüfung:** Sie läuft schon heute bei jedem Push, auch wenn nur Markdown geändert wurde
  (AK-7).

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
