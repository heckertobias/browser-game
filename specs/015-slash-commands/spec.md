# 015 – Slash-Commands (Test-Befehle)

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [011 – Chat](../011-chat/spec.md) (Chat-Eingabefeld; dass `/`-Text zunächst nur Text ist) ·
[012 – HP & Mana](../012-hp-mana/spec.md) (für `fullHealth()`) ·
[003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (für `resetPosition()`; Startpunkt nach 001)
Berührt: [011 – Chat](../011-chat/spec.md) – der Abfangpunkt sitzt im Chat-Versandweg

## Ziel

Im Chat kann jeder angemeldete Spieler Text eingeben, der mit `/` beginnt – z. B.
`/run fullHealth()` – der wird **nicht** an die anderen verteilt (er wird vor der Chat-Verteilung
abgefangen) und vom Server als **Command** verstanden. Der Server ruft die genannte Funktion aus
der Liste der bekannten Commands auf, und dessen Effekt tritt im Spielstand ein.

Für den ersten Lauf geht es nur um das **Testen der Mechanik**: Jeder angeschlossene Spieler darf
**jeden** Command ausführen, die Commands sind **überall aktiv**, und es gibt **keine
Berechtigungsstruktur**. Ein Command betrifft im ersten Lauf nur den **eigenen** Spieler, damit
der fehlende Schutz keine Probleme für andere macht. Die Syntax ist wie ein Funktionsaufruf
(`/run <name>()`) ausgelegt, damit später **Parameter** übergeben werden können, ohne die
Befehle umzustellen.

## User Stories

- Als **Tester / Spieler** möchte ich über dieselbe Stelle wie den Chat (011) Befehle eingeben,
  damit ich gezielt Dinge im Spiel auslöse, ohne die anderen mit Chat-Nachrichten zu behelligen.
- Als **Tester** möchte ich meinen eigenen HP- und Manawert per Befehl sofort wieder voll
  herstellen, damit ich Kampf- und Regenerationsflüsse (012) schnell prüfen kann.
- Als **Tester** möchte ich meinen Standort per Befehl zurück zum Startpunkt setzen, damit ich
  Positionsänderungen (003) wiederhole.
- Als **Entwickler** möchte ich, dass neue Commands wie Funktionen eingebaut werden, damit ich
  ihnen später Parameter übergeben kann, ohne die Befehls-Syntax umzustellen.

## Akzeptanzkriterien

### Eingabe & Abfangen
- **AK-1:** Gegeben ich bin im Spiel angemeldet (011), wenn ich in das Chat-Eingabefeld etwas
  eingebe, das mit `/` beginnt, dann wird es **nicht** als Chat-Nachricht an die anderen
  verteilt, sondern als Command behandelt (011-Plan: „Slash-Commands setzen genau hier an").
- **AK-2:** Gegeben ich eingebe etwas, das **nicht** mit `/` beginnt, dann wird es weiterhin wie
  eine normale Chat-Nachricht nach 011 verteilt (kein Regressionseffekt auf den Chat).

### Syntax
- **AK-3:** Gegeben ich eingebe die Form `/run <name>` (z. B. `/run fullHealth()`), dann versucht
  der Server, die benannte Funktion aus der Liste der bekannten Commands aufzurufen; die Syntax ist
  wie ein Funktionsaufruf (`/run <name>(…)`), damit später Parameter möglich werden. In diesem Lauf
  verwenden alle Commands keine Parameter (leere Parameterliste).
- **AK-4:** Gegeben ich eingebe mit einem `/` beginnende Eingabe, die keinem bekannten Command
  entspricht (auch nicht `/run`, z. B. `/xyz` oder ein unbekannter `/run …`-Name), dann sehe ich
  eine kurze Meldung (z. B. „Unbekannter Befehl") und es passiert im Spiel nichts.
- **AK-5:** Gegeben ein Command wird erkannt und ausgeführt, dann wird er **nicht** im Chat
  angezeigt (kein Chat-Echo); nur sein Effekt tritt im Spiel ein.

### Commands (1. Lauf)
- **AK-6:** Gegeben ich bin im Spiel, mein HP und/oder Mana ist unter dem Maximum, wenn ich
  `/run fullHealth()` eingebe, dann werden **mein** HP und **mein** Mana sofort auf das Maximum
  gesetzt (012), als wäre eine volle Erholung vorüber.
- **AK-7:** Gegeben ich bin an einer anderen Position, wenn ich `/run resetPosition()` eingebe,
  dann wechselt **meine** Figur sofort zum Startpunkt der Welt (wie beim ersten Start, vgl. 003
  AK-17 / 001).
- **AK-8:** Gegeben mehrere Spieler sind im Spiel und einer führt `resetPosition()` aus, dann
  sehen die anderen die neue Position innerhalb von etwa einer Sekunde (wie bei Bewegung, 001), da
  sich nur die Position des ausführenden Spielers ändert.

### Zugriff (bewusste Einschränkung)
- **AK-9:** Gegeben ich bin im Spiel angemeldet, dann kann ich **jeden** Command ausführen, ohne
  dass der Server prüft, ob ich dazu „berechtigt" bin; es gibt in diesem Lauf keine
  Berechtigungsprüfung.
- **AK-10:** Gegeben ein Command steht in der Liste, dann ist er in **jedem** Betrieb aktiv
  (z. B. in Test und in gleicher Weise im Produktivbetrieb); eine Umgebungs- oder Schalter-
  Abhängigkeit gibt es in diesem Lauf nicht.
- **AK-11:** Gegeben ein Command wird ausgeführt, dann trifft er im ersten Lauf **nur den
  ausführenden Spieler** (nicht andere Spieler, Gegner oder Dummies); andere Figuren in der Welt
  bleiben unberührt.

## Out of Scope

- **Berechtigungs- / Rollenstruktur** (wer darf welchen Command, wer ist Admin/Moderator) –
  bewusste, dokumentierte Einschränkung; kommt mit einer eigenen späteren Spec.
- Commands, die **andere** Figuren gezielt betreffen (z. B. `fullHealth(ziel)`), mehrere Ziele
  oder gegen- bzw. dummyseitige Commands (z. B. `spawnDummy`, `tp`) – später.
- **Umgebungs-Umschaltung** (Test vs. Produktiv) oder ein An-/Aus-Schalter pro Command – später.
- **Parameter** für Commands (die Syntax ist darauf ausgelegt, wird aber in diesem Lauf nicht
  genutzt).
- Command-Verlauf / -History, Alias- oder Autovervollständigung im Eingabefeld, ein
  „Hilfs-Command".
- Ein eigener `debug`-Namensraum oder andere Präfixe außer `/run`; `/run` ist der einzige
  Dispatcher, `/debug` wäre „nur" eine Funktion (im ersten Lauf nicht vorhanden).
- **Kurzworte / Aliase (Vormerkung, später – nicht jetzt, nicht für alle):** Später sollen
  Kurzworte den Funktionsaufruf abkürzen können, z. B. `/debug A B C` → `/run debug(A, B, C)`.
  Bewusst weder jetzt noch pauschal für alle Commands und ohne konkreten Bedarf; die parametrische
  Syntax (`/run <name>(…)`) macht das später ohne Umbau möglich.
- Speichern oder Loggen der eingegebenen Commands.
- Weitere Commands neben den beiden im 1. Lauf; die Mechanik ist so aufgebaut, dass weitere leicht
  ergänzt werden können.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Dispatcher `/run`:** Der einzige Befehls-Präfix ist `/run`; dahinter steht der Funktionsname,
  optional mit `(…)` für Parameter. Die früher getrennten Formen (`/debug …` und `/run …`) werden
  so vereinheitlicht: `/debug full_health` wird zur Funktion `fullHealth()`, aufgerufen über
  `/run fullHealth()`.
- **Zwei Commands im 1. Lauf:** `fullHealth()` (setzt die **eigenen** HP und Mana auf das
  Maximum, 012) und `resetPosition()` (setzt die **eigene** Position zurück zum Startpunkt der
  Welt, 001/003).
- **Command-Effekte sind nicht im Chat sichtbar (AK-5):** Es wirkt sich nur im Spiel aus und wird
  nicht an andere Spieler gesendet.
- **Eingabefeld = 011-Chat-Eingabefeld, nur Angemeldete:** Der Abfangpunkt sitzt im
  Chat-Versandweg der 011, bevor eine Nachricht verteilt wird. Kein eigenes Dev-Eingabefeld.
- **Keine Berechtigungen (AK-9) und immer aktiv (AK-10):** bewusste Einschränkung für den
  Testen-Lauf. Befehle betreffen nur den eigenen Spieler (AK-11), damit der fehlende Schutz
  (noch) kein Problem auslöst.
- **Server ist Autorität** (Constitution 2): nur der Server kennt die Liste der Commands und führt
  sie aus; der Client sieht nur den Effekt und/oder die „Unbekannter Befehl"-Meldung.
- **Parameter-Support ist vorhanden, aber ungenutzt:** Die Syntax `/run <name>(…)` ist
  parameterfähig (Architektur); die im 1. Lauf genutzten Commands haben keine Parameter.
- **Fehlerverhalten:** Unbekannter oder fehlerhafter Befehl → kurze Fehlermeldung, keine Wirkung
  (AK-4).

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
