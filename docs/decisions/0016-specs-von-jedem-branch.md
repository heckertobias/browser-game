# ADR 0016 – Specs von jedem Branch

**Status:** akzeptiert – ändert [ADR 0011](0011-branch-namen.md) für neue Specs

## Kontext

Nach ADR 0011 entsteht jede Spec auf einem eigenen `spec/`-Branch. Ideen und Probleme fallen aber meist bei anderer
Arbeit auf: beim Umsetzen, beim Review, beim Aufräumen. Dafür eigens einen Branch und einen PR anzulegen, wird
aufgeschoben, und dann ist der Gedanke vergessen. Specs sind die User Stories des Projekts; sie sollen sofort
festgehalten werden können, wo die Idee entsteht.

## Entscheidung

1. **Eine neue `spec.md` darf auf jedem Branch entstehen** (`spec/`, `impl/`, `chore/`), samt Eintrag in der Roadmap.
   Sie hält eine Idee oder ein Problem für später fest. Ist noch etwas unklar, bleibt sie im Status `entwurf` mit
   offenen Fragen; `bereit` gilt wie sonst erst ohne offene Fragen.
2. **`plan.md` und `tasks.md` entstehen erstmals auf einem `spec/NNN-name`-Branch**, wie bisher getrennt von der
   Umsetzung. Danach darf der `impl/`-Branch sie anpassen (ADR 0011).
3. **Die Nummer einer neuen Spec ist die nächste freie auf `main`.** Weil jetzt mehrere Branches gleichzeitig Specs
   anlegen können, prüft die CI, dass die Nummer auf `main` noch nicht belegt ist
   ([026](../../specs/026-spec-nummer-eindeutig/spec.md)). Bis das umgesetzt ist, prüft man es vor dem Merge selbst.

Alles andere aus ADR 0011 bleibt: Bestehende Specs ändert ein `spec/`-Branch, während der Umsetzung der `impl/`-Branch.

## Konsequenzen

- Eine neue Spec kann mit einem `impl/`- oder `chore/`-PR nach `main` kommen. Das Review dieses PRs prüft sie mit,
  genau wie den Code.
- Laufen zwei Branches mit derselben neuen Nummer, gewinnt der zuerst gemergte; der andere nummeriert um.
- Der Weg „Idee → Spec“ braucht keinen Kontextwechsel mehr; „Spec → Plan“ bleibt ein eigener, bewusster Schritt.

## Alternativen

- **GitHub Issues für Ideen:** liegen außerhalb des Repos, Agenten sehen sie nicht beim Lesen der Specs, und aus
  einem Issue muss später trotzdem eine Spec werden.
- **Sammeldatei für Ideen (z. B. `IDEAS.md`):** ohne Nummer, Status und Roadmap; Einträge veralten unbemerkt.
- **Bei ADR 0011 bleiben:** sauber getrennt, aber Ideen gehen verloren, weil der zusätzliche Branch zu teuer ist.
