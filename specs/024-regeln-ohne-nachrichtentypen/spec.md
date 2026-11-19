# 024 – Spielregeln ohne Nachrichtentypen

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [002 – Tastenbelegung](../002-keybindings/spec.md) (Prüfen und Speichern der Belegung) ·
[013 – Kampfsystem](../013-kampfsystem/spec.md) (Angriffsarten, zielbare Objekte)

## Ziel

Die Spielregeln des Servers (Kampf, Prüfen der Tastenbelegung) arbeiten heute direkt mit den Typen, die aus den
Verträgen für die Übertragung erzeugt werden. Damit hängen die Regeln an der Form der Nachrichten: Ein geändertes
Nachrichtenformat berührt die Regeln, und wer die Regeln liest, muss das Format kennen. Ziel ist, dass die Regeln nur
eigene Begriffe des Spiels kennen und die Übersetzung an der Grenze zum Netz passiert.

Ob das sinnvoll ist, hängt an den offenen Fragen; ohne Antwort darauf wird diese Spec nicht umgesetzt.

## User Stories

- Als **Entwickler** möchte ich die Spielregeln lesen und testen, ohne das Nachrichtenformat zu kennen, damit Regeln
  und Übertragung getrennt bleiben.
- Als **Entwickler** möchte ich ein Nachrichtenformat ändern können, ohne Spielregeln anzufassen, die das neue Feld
  nicht betrifft.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Umstellung, dann verhält sich das Spiel für Spieler unverändert; alle bestehenden automatischen
  Tests bleiben grün.
- **AK-2:** Gegeben die Spielregeln, dann lassen sie sich ohne Nachrichtentypen prüfen: Tests der Regeln sprechen nur
  Begriffe des Spiels.
- **AK-3:** Gegeben ein Wert, den das Spiel nicht kennt (z. B. eine unbekannte Angriffsart), dann wird er an der Grenze
  zum Netz abgewiesen, mit derselben Wirkung wie heute (013 AK-12).

## Out of Scope

- Änderungen an den Verträgen selbst.
- Der Client: Dort liegen Regeln und Darstellung bereits getrennt von der Übertragung, soweit nötig.
- Neue Regeln oder geändertes Verhalten.

## Offene Fragen

- [ ] **Constitution §3:** „Typen … werden generiert, nie von Hand dupliziert.“ Eigene Begriffe in den Regeln, etwa
  eine Angriffsart mit Nah- und Fernkampf, hätten dieselben Werte wie der Vertrag. Gilt §3 nur für Nachrichten (dann
  braucht es eine ADR, die das klarstellt) oder auch für die Regeln (dann entfällt diese Spec)?
- [ ] **Lohnt sich das jetzt?** Constitution §5 verbietet Abstraktion auf Vorrat. Heute gibt es keinen konkreten
  Schaden; ein Anlass wäre z. B. eine zweite Vertragsversion oder ein Client, der andere Nachrichten spricht. Soll die
  Spec bis dahin ruhen?
- [ ] **Tastenbelegung:** Sie wird bewusst als Vertragsnachricht gespeichert (002). Gehört ihre Prüfung dazu, oder
  bleibt sie ausgenommen, weil Speicherformat und Vertrag dort absichtlich gleich sind?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
