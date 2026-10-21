# Contracts

Einzige Quelle für Datentypen zwischen Server und Client.

- `openapi/` – HTTP-API (Accounts, Landing, Verwaltung)
- `proto/` – WebSocket-Nachrichten (Bewegung, Kampf, Welt-Updates)

Regeln:
- Änderungen hier werden im `plan.md` des Features festgelegt, bevor Code entsteht.
- C#- und TypeScript-Typen werden daraus generiert – nie von Hand schreiben.
