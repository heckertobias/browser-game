# 013 – Rudimentäres Kampfsystem

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [012 – HP & Mana](../012-hp-mana/spec.md)
Voraussetzung für: [014 – Gegner](../014-gegner/spec.md) (geplant)

## Ziel

Der Spieler kann etwas in der Welt angreifen. Er wählt ein Ziel aus – für diese Spec der
Trainings-Dummy – und kann es mit einem von zwei Angriffen treffen: **Nahkampf** (gratis, kurze
Reichweite, Treffen sofort) und **Fernkampf** (kostet Mana, größere Reichweite, ein Projektil mit
Flugzeit). Der Dummy nimmt Schaden und stellt sich wieder her. Zwischen zwei Angriffen liegt eine
kurze, **allgemeine Abklingzeit (Cooldown)**, damit kein Angriff ohne Wartezeit den anderen
verdrängt.

Das ist der dünnste Kampf-Durchstich: **Zielauswahl, zwei Angriffstypen und „Dingen Schaden
zufügen"** laufen Ende-zu-Ende (Client schlägt vor, Server entscheidet). Der Spieler selbst lernt
hier noch **keinen Schaden** nehmen – das kommt mit den Gegnern in 014.

## User Stories

- Als **Spieler** möchte ich ein Ziel anwählen können (antippen oder anklicken), damit ich sicher
  weiß, was ich angreife.
- Als **Spieler** möchte ich ein anvisiertes Ziel mit einem Nah- und mit einem Fernangriff treffen,
  damit ich sehe, wie Kampf funktioniert.
- Als **Spieler** möchte ich, dass mein Fernkampf Mana kostet und ich bei zu wenig Mana kurz gewarnt
  werde, damit ich mein Vorgehen anpassen kann.
- Als **Spieler** möchte ich zwischen zwei Angriffen eine kurze Wartezeit einhalten, damit das
  Kampftempo fair und überschaubar bleibt.
- Als **Spieler / Tester** möchte ich einen Trainings-Dummy, der Schaden nimmt und sich wieder
  herstellt, damit ich unabhängig von echten Gegnern (014) Angriffe üben und prüfen kann.

## Akzeptanzkriterien

### Zielauswahl
- **AK-1:** Gegeben ein zielbares Objekt (der Trainings-Dummy) ist sichtbar, wenn ich es antippe
  (Handy) bzw. anklicke (PC), dann wird es zu meinem aktiven Ziel und visuell hervorgehoben
  (z. B. umrandet).
- **AK-2:** Gegeben es ist kein Ziel markiert, wenn ich einen Angriff (Nah- oder Fernkampf) auslöse,
  dann passiert nichts: es entsteht kein Angriff.
- **AK-3:** Gegeben ich habe ein Ziel markiert, wenn ich ein anderes zielbares Objekt anwähle, dann
  wird das neue zum aktiven Ziel; es gibt jederzeit höchstens ein aktives Ziel.

### Nahkampf
- **AK-4:** Gegeben ich habe ein Ziel markiert, wenn ich den Nahkampf-Slot auslöse und mir das Ziel
  in der festen Nahkampfreichweite (Konstante im Plan) ist, dann erleidet das Ziel sofort den
  Nahkampfschaden (feste Konstante im Plan).
- **AK-5:** Gegeben ich habe ein Ziel markiert, wenn ich den Nahkampf auslöse und das Ziel außerhalb
  der Nahkampfreichweite ist, dann passiert nichts (kein Schaden).
- **AK-6:** Gegeben ich löse einen Nahkampf aus, dann wird kein Mana verbraucht.

### Fernkampf
- **AK-7:** Gegeben ich habe ein Ziel in Reichweite markiert, wenn ich den Fernkampf-Slot auslöse und
  genug Mana habe, dann wird ein Projektil auf das Ziel gestartet; sobald es einschlägt, erleidet
  das Ziel den Fernkampfschaden (feste Konstante im Plan).
- **AK-8:** Gegeben ich habe ein Ziel in Reichweite markiert, wenn ich den Fernkampf auslöse und
  nicht genug Mana habe, dann wird kein Projektil gestartet, ich erhalte einen kurzen Hinweis
  (Feedback, z. B. kurzes Icon/Text „zu wenig Mana") und mein Mana wird nicht verändert.
- **AK-9:** Gegeben ich habe ein Ziel markiert, wenn ich den Fernkampf auslöse und das Ziel
  außerhalb der Maximalreichweite (Konstante im Plan) ist, dann wird kein Projektil gestartet (kein
  Schaden).
- **AK-10:** Gegeben ein Fernkampf greift zu, dann wird eine feste Mana-Menge (Konstante im Plan)
  verbraucht.

### Cooldown
- **AK-11:** Gegeben ich habe gerade einen Angriff ausgeführt (Nahkampf hat getroffen oder ein
  Fernkampf-Projektil ist gestartet worden), dann warte ich eine feste Abklingzeit (Konstante im
  Plan) auf, bevor ich den nächsten Angriff ausführen kann. Die Abklingzeit gilt für jeden
  Angriffstyp gleichermaßen (ein allgemeiner Cooldown).
- **AK-12:** Gegeben ein Angriffsversuch schlägt fehl, weil kein Ziel anvisiert ist (AK-2), nicht in
  Reichweite (AK-5 / AK-9) oder zu wenig Mana (AK-8), dann startet keine Abklingzeit: Ein
  fehlgeschlagener Versuch sperrt mich nicht aus.

### Trainings-Dummy
- **AK-13:** Gegeben der Spieler ist im Spiel, dann ist genau ein Trainings-Dummy an einem festen Ort
  vorhanden (feste Startposition und feste Start-HP, beide Konstanten im Plan).
- **AK-14:** Gegeben der Trainings-Dummy ist mein Ziel, wenn ihn ein Nah- oder Fernangriff trifft,
  dann sinken seine HP um den zugefügten Schaden (AK-4 bzw. AK-7).
- **AK-15:** Gegeben der Trainings-Dummy hat 0 HP erreicht, dann stellt er sich nach einer (im Plan)
  festen Verzögerung wieder mit vollen HP her (Respawn) und ist wieder angreifbar.
- **AK-16:** Gegeben der Trainings-Dummy wird getroffen, dann fügt er im ersten Durchstich dem
  Spieler keinen Schaden zu (das kommt in 014).
- **AK-17:** Gegeben der Spieler startet das Spiel frisch, dann existiert der Trainings-Dummy neu; er
  wird nicht gespeichert.
- **AK-18:** Gegeben der Trainings-Dummy ist sichtbar, dann ist sein aktueller HP-Wert erkennbar
  (z. B. ein Balken über ihm), damit der Spieler sieht, ob ein Angriff etwas gewirkt hat.

## Out of Scope

- Schaden, den der Spieler erleidet, oder Tod des Spielers (→ 014, baut auf 012 auf).
- Echte Gegner oder weitere zielbare Objekte (→ 014); für diese Spec ist der Trainings-Dummy das
  einzige Ziel.
- Variabler oder pro Zauber / pro Status einstellbarer Cooldown; der Cooldown wird in dieser Spec
  als **eine feste Konstante** geführt (Standardwert im Plan), später ggf. durch Statuswerte
  anpassbar (eigene Spec).
- Kritische Treffer, Schadensmultiplikatoren, Status- oder Ketteneffekte.
- Schadenszahlen oder ein Kampf-Log.
- Mehrere Dummy-Ziele oder eine frei wählbare Auslage; es gibt genau einen, an einer festen Stelle.
- Ein Ziel-Reticule oder Richtungssteuerung per Maus; „zielen" heißt hier: das Ziel antippen /
  anklicken (AK-1).
- Persistenz des Dummies oder des Kampfs (der Dummy lebt nicht zwischen Sessions).
- Projektile, die andere Spieler oder echte Gegner treffen (der einzige angreifbare und zielbare
  Gegenstand ist der Dummy).
- Terrain; in dieser Spec kann ein Projektil mit nichts anderem in Konflikt kommen.
- Mana- oder HP-Quellen abgesehen von der Regeneration gemäß 012.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Zielauswahl zuerst:** Ohne anvisiertes Ziel gibt es keinen Angriff (AK-2). „Zielen" heißt
  antippen (Handy) bzw. anklicken (PC); es ist gleichzeitig nur ein Ziel aktiv (AK-3). Das
  Ziel-Modell ist bewusst generell gehalten (ein „zielbares Objekt"), damit in 014 auch Gegner
  angreifbar werden.
- **Zwei feste Angriffsslots** (das PC/Handy-Mittelmaß, siehe README/012): **Nahkampf** (gratis,
  kurze Reichweite, Treffen sofort) und **Fernkampf** (kostet Mana, größere Reichweite, Projektil
  mit Flugzeit). Die Anzahl der Slots bleibt klein und ändert sich **nicht pro Zauber**, wenn
  später mehr Zauber dazukommen – man zieht sie in die wenigen Slots, statt viele Buttons ins
  Gesicht zu bekommen.
- **Fernkampf** kostet fixe Mana, hat eine feste Maximalreichweite und feuert ein Projektil.
  **Kein Cast** bei zu wenig Mana (mit Feedback und ohne Mana-Abzug, AK-8) oder außerhalb der
  Reichweite (AK-9).
- **Nahkampf** kostet kein Mana (AK-6) und trifft nur innerhalb der Nahkampfreichweite (AK-4 / 5).
- **Allgemeiner Cooldown (AK-11 / 12):** Ein einzelner, für alle Angriffstypen gleichermaßen
  geltender Cooldown, gemessen ab dem Auslösen eines erfolgreichen Angriffs. Er ist in dieser Spec
  eine feste Konstante (Standardwert im Plan, jetzt **1 Sekunde**); variable oder durch Statuswerte
  anpassbare Cooldowns kommen später in einer eigenen Spec. Ein fehlgeschlagener Versuch (kein Ziel,
  keine Reichweite, zu wenig Mana) startet den Cooldown nicht, damit er mich nicht blockiert (AK-12).
- **Trainings-Dummy:** genau einer, feste Position, feste Start-HP, fügt im ersten Durchstich keinen
  Schaden zu (AK-16), wird nicht gespeichert (AK-17) und ist das einzige Ziel in dieser Spec.
  Bei 0 HP stellt er sich nach einer festen Verzögerung wieder her (AK-15); stirbt er, wird das
  anvisierte Ziel aufgehoben, nach dem Respawn kann er erneut ausgewählt werden.
- **Server ist Autorität** (Constitution 2): Der Client schlägt „Ziel anwählen" und „Angriff
  auslösen" vor; der Server prüft Ziel-Validität, Reichweite, Mana und Cooldown, rechnet Schaden
  aus und steuert den Respawn. Die Werte des Zieles (Position, HP) führt der Server; wie sie zum
  Client gelangen, regeln die Verträge im Plan (Regeneration der Werte nach 012).
- **Alle Zahlen sind Konstanten im Plan** (Nahkampfreichweite, Maximalreichweite, Nah- und
  Fernkampfschaden, Mana-Kosten des Fernkampfes, die Cooldown-Abklingzeit, Respawn-Verzögerung,
  Start-HP des Dummies); die Spec nennt keine Zahlen.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
