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

Spec, Plan und Tasks entstehen auf dem Branch `spec/NNN-name` und werden per PR (Squash) nach `main` gemergt –
meist lange bevor die Umsetzung beginnt.

### 4. Umsetzung
- Von `main` den Branch `impl/NNN-name` anlegen und einen Draft-PR gegen `main` öffnen (nie direkt auf `main`).
- Status in der Spec auf `in arbeit` setzen, Roadmap mitpflegen.
- Task für Task abarbeiten, abhaken, committen und pushen.
- Zum Testen in der Testumgebung: `scripts/release.sh X.Y.Z-rc.1` vom Branch. Gibt es Fehler: beheben, `-rc.2` usw.
- Stellt sich beim Bauen heraus, dass die Spec falsch war: **erst Spec ändern, dann Code.**
- Am Ende: alle Akzeptanzkriterien durchgehen, Status in der Spec auf `umgesetzt` setzen, Roadmap aktualisieren,
  PR auf „Ready“ stellen und mit **Merge-Commit** mergen. Danach `scripts/release.sh X.Y.Z` auf `main`.
- Tooling und Doku (Skripte, ADRs, READMEs) laufen auf einem kurzlebigen Branch `chore/kurzer-name` mit PR (Squash).

### Branch-Namen ([ADR 0011](../docs/decisions/0011-branch-namen.md))

| Präfix | Wofür | Merge |
|---|---|---|
| `spec/NNN-name` | Spec schreiben oder ändern (nur `specs/`) | Squash |
| `impl/NNN-name` | Spec umsetzen (Draft-PR, Release Candidates) | Merge-Commit |
| `chore/kurzer-name` | Tooling, CI, Skripte, ADRs, READMEs | Squash |

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
- `scripts/check-docs.py` prüft Links und diese Regeln (Status, Roadmap, offene Fragen, AK-Bezüge, Tasks); die CI
  führt es bei jedem Push aus. Ändert ein Push nur Markdown, entfallen Build, Tests und Images.

## Roadmap

Die Nummer ist nur eine ID (Reihenfolge des Anlegens). Umgesetzt wird nach Status und Abhängigkeiten:
nur `bereit`, und erst wenn alles unter „setzt voraus“ `umgesetzt` ist. Beim Statuswechsel einer Spec hier mitpflegen.

| Reihenfolge | Spec | Status | Setzt voraus |
|---|---|---|---|
| – | [000 – Vision](000-vision/spec.md) | entwurf (wächst mit) | – |
| 1 | [001 – Walking Skeleton](001-walking-skeleton/spec.md) | umgesetzt | – |
| 2 | [004 – Admin-Interface & SMTP-Einstellungen](004-admin/spec.md) | umgesetzt | 001 |
| 3 | [003 – Accounts & gespeicherte Position](003-accounts/spec.md) | umgesetzt | 004 |
| 4 | [006 – Deutsch und Englisch](006-sprachen/spec.md) | umgesetzt | 001, 004, 003 bis T7 |
| 5 | [007 – Handy hochkant, Sichtfeld ohne Ränder, Web-App](007-mobil/spec.md) | in arbeit | 001, 006 |
| 6 | [005 – Sachliches Layout fürs Admin-Interface](005-admin-layout/spec.md) | umgesetzt | 004 |
| 7 | [011 – Chat](011-chat/spec.md) | in arbeit | 003, 007 (Code auf `main`, nur die Android-Prüfung fehlt) |
| 8 | [002 – Tastenbelegung & erstes Menü](002-keybindings/spec.md) | umgesetzt | 003 |
| 9 | [012 – HP & Mana](012-hp-mana/spec.md) | umgesetzt | 001 |
| 10 | [016 – Blickrichtung](016-blickrichtung/spec.md) | umgesetzt | 001 |
| 11 | [013 – Rudimentäres Kampfsystem](013-kampfsystem/spec.md) | bereit | 012, 016 |
| 12 | [015 – Slash-Commands (Test-Befehle)](015-slash-commands/spec.md) | bereit | 011, 012, 003 |
| 13 | [014 – Gegner (erstes Ziel, das HP hat und zurückwirkt)](014-gegner/spec.md) | bereit | 012, 013, 015, 016 |
| 14 | [017 – Spielfigur als Pixel-Art](017-spielfigur/spec.md) | entwurf | 001, 003, 015, 016 |
| 15 | [018 – Boden statt Gitter](018-boden/spec.md) | entwurf | 017 |
| 16 | [019 – Deko-Objekte](019-deko/spec.md) | entwurf | 018 |
| 17 | [020 – Pixel-Schrift](020-pixel-schrift/spec.md) | entwurf | 001 |
| später | Haus des Spielers / Offline-Produktion | noch keine Spec | 003 |
| später | [008 – Push-Benachrichtigungen](008-push/spec.md) | entwurf | 007, Haus |
| nach Release | [009 – Wechsel auf .NET 11](009-dotnet-11/spec.md) | entwurf | .NET 11 GA + 7 Tage |
| nach Release | [010 – Wechsel auf .NET 12](010-dotnet-12/spec.md) | entwurf | 009, .NET 12 GA + 7 Tage |
