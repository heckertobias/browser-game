# ADR 0012 – Deploy außerhalb des Repos

**Status:** akzeptiert – ersetzt Punkt 5–7 von [ADR 0009](0009-spec-branches-rc-und-deploy-per-api.md)

## Kontext

ADR 0009 legte das Deploy-Skript ins Repo. Es war fest auf die Container-Verwaltung der privaten Testumgebung
zugeschnitten, und Doku, ADRs und Specs beschrieben diese Umgebung (Werkzeuge, Hostnamen, Hardware, Backup). Das
öffentliche Repo soll das Spiel beschreiben, nicht die Umgebung, in der es gerade getestet wird.

## Entscheidung

1. **Kein Deploy-Skript im Repo.** `scripts/release.sh` endet nach Tag, Images und GitHub Release.
2. **Lokaler Hook:** Ist `~/.config/browser-game/deploy` (oder `$BROWSER_GAME_DEPLOY`) ausführbar, ruft `release.sh` ihn
   im Repo-Verzeichnis mit gesetztem `BROWSER_GAME_REPO` auf:
   - `--check` vor dem Tag; ein Fehler bricht ab, bevor etwas veröffentlicht ist.
   - `--dry-run --unreleased VERSION` bei `release.sh --dry-run`.
   - `VERSION` nach CI und GitHub Release.
3. **Compose:** `deploy/compose.yaml` bleibt als Beispiel für den Betrieb hinter einem Reverse-Proxy, mit
   `${GAME_VERSION}` in den `image:`-Zeilen. Jeder Release-Tag enthält sie.
4. **Keine Umgebungsdetails im Repo:** keine Namen von Werkzeugen, Hosts, Domains oder Geräten der Testumgebung.
   Specs schreiben „Testumgebung“; Tests verwenden `example.*`-Namen.

## Konsequenzen

- Der Hook ist nicht versioniert; seine Pflege liegt beim Betreiber.
- `release.sh` hält Netzwerkfehler beim Abfragen der CI aus und wertet sie nicht als CI-Fehler.
- Ältere Commits und Tags enthalten die Details weiterhin; die Historie wird nicht umgeschrieben.

## Alternativen

- **Generisches Deploy-Skript im Repo:** bliebe an eine bestimmte Container-Verwaltung gebunden, nur ohne ihren Namen.
- **Deploy aus der CI:** bräuchte Zugang zur Testumgebung in GitHub (ADR 0002: pull-basiert).
