# Spec-Driven Development – So arbeiten wir

## Die Idee in einem Satz

Erst wird aufgeschrieben, **was** ein Feature leisten soll und woran man erkennt, dass es fertig ist.
Dann wird geplant, **wie** es gebaut wird. Erst danach wird Code geschrieben – und zwar gegen die Spec, nicht gegen das Bauchgefühl.

Das zahlt sich besonders aus, wenn KI-Agenten mitprogrammieren: Eine gute Spec ist der beste Prompt.

## Die vier Phasen

### 1. Spec (`spec.md`) – WAS und WARUM
- Aus Spieler- bzw. Nutzersicht geschrieben, **keine Technik** (kein "WebSocket", kein "EF Core").
- User Stories + **Akzeptanzkriterien** im Format *Gegeben / Wenn / Dann*.
- Alles Unklare kommt unter **Offene Fragen**. Eine Spec mit offenen Fragen ist nicht fertig.
- Explizit festhalten, was **nicht** dazugehört (Out of Scope) – das ist die halbe Miete.

✅ Fertig, wenn: keine offenen Fragen mehr, jedes Kriterium ist prüfbar.

### 2. Plan (`plan.md`) – WIE
- Jetzt kommt Technik: welche Komponenten, welche Nachrichten, welche Daten.
- Änderungen an `contracts/` (Proto/OpenAPI) werden **hier zuerst** festgelegt.
- Gegen die `constitution.md` prüfen. Weicht der Plan ab → begründen oder ADR schreiben.

✅ Fertig, wenn: jedes Akzeptanzkriterium der Spec ist durch den Plan abgedeckt.

### 3. Tasks (`tasks.md`) – Schritte
- Plan in kleine, einzeln prüfbare Schritte zerlegen (je max. ein paar Stunden).
- Reihenfolge: Verträge → Server → Client → Tests.
- Jeder Task verweist auf das Akzeptanzkriterium, das er erfüllt (z. B. `AK-2`).

### 4. Umsetzung
- Task für Task abarbeiten, abhaken, committen.
- Stellt sich beim Bauen heraus, dass die Spec falsch war: **erst Spec ändern, dann Code.**
- Am Ende: alle Akzeptanzkriterien durchgehen, Status in der Spec auf `umgesetzt` setzen.

## Neues Feature anlegen

```bash
cp -r specs/_template specs/002-mein-feature
```

Nummern fortlaufend, Namen kurz und sprechend.

## Status-Werte (Kopf jeder spec.md)

`entwurf` → `bereit` (keine offenen Fragen) → `in arbeit` → `umgesetzt`

## Faustregeln

- Lieber viele kleine Specs als eine riesige.
- Die erste Spec ist ein **Walking Skeleton**: der dünnste Durchstich durch alle Schichten.
- Verträge (`contracts/`) sind ausführbare Specs – aus ihnen wird Code generiert, nie umgekehrt.
- Größere, schwer umkehrbare Entscheidungen → ADR in `docs/decisions/`.

## Roadmap

Die Nummer ist nur eine ID (Reihenfolge des Anlegens). Umgesetzt wird nach Status und Abhängigkeiten:
nur `bereit`, und erst wenn alles unter „setzt voraus“ `umgesetzt` ist. Beim Statuswechsel einer Spec hier mitpflegen.

| Reihenfolge | Spec | Status | Setzt voraus |
|---|---|---|---|
| – | [000 – Vision](000-vision/spec.md) | entwurf (wächst mit) | – |
| 1 | [001 – Walking Skeleton](001-walking-skeleton/spec.md) | umgesetzt | – |
| 2 | [004 – Admin-Interface & SMTP-Einstellungen](004-admin/spec.md) | bereit | 001 |
| 3 | [003 – Accounts & gespeicherte Position](003-accounts/spec.md) | in arbeit (pausiert für 004) | 004 |
| 4 | [002 – Tastenbelegung & erstes Menü](002-keybindings/spec.md) | bereit | 003 |
| später | Haus des Spielers / Offline-Produktion | noch keine Spec | 003 |
