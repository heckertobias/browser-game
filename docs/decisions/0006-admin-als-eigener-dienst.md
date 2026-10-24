# ADR 0006 – Admin-Interface als eigener Dienst mit gemeinsamer Datenbank

**Status:** akzeptiert

## Kontext

Das Spiel soll von Anfang an über ein Admin-Interface betrieben werden (004), unabhängig vom Spiel lauffähig und nur
intern erreichbar. Admin und Spiel müssen sich Daten teilen (zuerst die SMTP-Einstellungen, später Spieler,
Spielwerte), ohne dass eins vom laufenden anderen abhängt.

## Entscheidung

- **Eigenes Projekt und eigenes Image** `ghcr.io/heckertobias/browser-game-admin` (ASP.NET Core, Razor Pages),
  eigener Container, eigener interner Hostname. Kein veröffentlichter Port; Zugriff nur über nginx-proxy.
- **Gemeinsame Datenbank, keine Kommunikation über das Netz** zwischen Admin und Spiel: Das Admin schreibt
  Einstellungen in die DB, das Spiel liest sie bei Bedarf. Fällt eins aus, läuft das andere weiter.
- **Gemeinsame Datenschicht** als Bibliothek `BrowserGame.Data`:
  - `GameDbContext`: Spieler, Positionen, Einstellungen, Data-Protection-Schlüssel.
  - `AdminDbContext`: Admin-Accounts (eigenes Identity-Modell mit Tabellenpräfix `Admin`), Einladungen, Protokoll.
  - Beide Kontexte nutzen dieselbe Datenbank mit getrennten Migrations-Historien. Beide Dienste wenden beim Start die
    Migrationen an, die sie brauchen; EF Core sperrt dabei parallele Migrationen.
- **Geheimnisse** (SMTP-Passwort) werden mit ASP.NET Data Protection verschlüsselt gespeichert. Beide Dienste nutzen
  denselben Schlüsselring (in der DB) und denselben Zweck `Settings.Smtp`; Anmelde-Cookies verwenden getrennte Schemas,
  ein Spieler-Cookie ist im Admin also wertlos und umgekehrt.
- **Versionen laufen gemeinsam:** Beide Images entstehen aus demselben Commit und demselben Release-Tag; die Compose
  pinnt beide auf dieselbe Version.

## Alternativen

- Admin als Bereich im Spiel-Server: einfacher, aber nicht unabhängig (Absturz des Spiels = kein Admin) und mit
  derselben Angriffsfläche nach außen.
- Admin spricht über eine API mit dem Spiel: entkoppelt die Datenbank, aber das Admin wäre ohne laufendes Spiel nutzlos.

## Konsequenzen

- Bei SQLite greifen zwei Prozesse auf dieselbe Datei zu. Das funktioniert im WAL-Modus auf einem lokalen Volume
  desselben Hosts. Für mehrere Hosts ist PostgreSQL nötig (ADR 0004).
- Schemaänderungen müssen zu beiden Diensten passen. Da beide aus demselben Commit kommen, wird nur gemeinsam aktualisiert.
- CI baut, scannt und veröffentlicht zwei Images.
