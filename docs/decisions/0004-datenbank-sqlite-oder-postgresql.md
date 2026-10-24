# ADR 0004 – Datenbank: SQLite oder PostgreSQL per Konfiguration

**Status:** akzeptiert

## Kontext

Mit 003 (Accounts) braucht das Spiel zum ersten Mal Persistenz. Betrieben wird es vorerst auf einem Raspberry Pi
(ein Server, ≤ 50 Spieler). Später sollen mehr Spieler, mehrere Instanzen oder Auswertungen möglich sein, ohne
das Spiel umzubauen. Auf dem Pi ist SD-Karten-Verschleiß ein reales Risiko (siehe Homelab, th-rpi-02).

## Entscheidung

- Zugriff ausschließlich über **EF Core**; die Datenbank wird per Konfiguration gewählt:
  `Database__Provider=Sqlite` (Standard) oder `Database__Provider=Postgres`, dazu `ConnectionStrings__Game`.
- Beide Provider werden **gleichwertig** gepflegt: je ein eigener Migrationssatz
  (`Data/Migrations/Sqlite`, `Data/Migrations/Postgres`), erzeugt über je einen abgeleiteten DbContext.
- Die CI führt die Integrationstests gegen **beide** Datenbanken aus (PostgreSQL als Service-Container).
- Deployment auf dem Pi: **SQLite** im Volume (`/data/game.db`, WAL-Modus). Für ein konsistentes Backup schreibt der
  Server nachts eine Kopie per `VACUUM INTO` (vor dem restic-Lauf des Homelabs).
- Migrationen laufen beim Start automatisch (nur eine Instanz).

## Alternativen

- Nur SQLite: einfacher, aber späterer Wechsel bräuchte dann erst einen zweiten Migrationssatz.
- Nur PostgreSQL: robuster für Skalierung, aber eigener Container, mehr Betrieb und mehr Schreiblast auf der SD-Karte.
- MariaDB/MySQL, SQL Server, Dokument-DBs: siehe Diskussion zu 003 – kein Vorteil bzw. nicht auf ARM oder ohne Identity-Store.

## Konsequenzen

- Jede Schemaänderung erzeugt **zwei** Migrationen; Code darf keine providerspezifischen SQL-Features voraussetzen
  (Ausnahme: das SQLite-Backup).
- Bewusste Abweichung von Constitution §5 (KISS, keine Abstraktion auf Vorrat): ausdrücklicher Wunsch, später ohne
  Umbau auf PostgreSQL wechseln zu können.
- Wechsel auf PostgreSQL, sobald mehrere Instanzen, deutlich mehr Spieler oder parallele Auswertungen anstehen;
  Datenumzug dann per Export/Import (eigene Aufgabe).
