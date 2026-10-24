# ADR 0005 – Anmeldung mit ASP.NET Core Identity (Passwort + Passkeys), Mail per SMTP

**Status:** akzeptiert

## Kontext

003 verlangt Accounts mit E-Mail + Passwort oder Passkey, E-Mail-Bestätigung, Passwort vergessen, Sperre bei
vielen Fehlversuchen und Account löschen. Selbst gebaute Authentifizierung ist ein großes Sicherheitsrisiko.

## Entscheidung

- **ASP.NET Core Identity** (.NET 10) mit EF-Core-Store. Identity bringt Passwort-Hashing, Lockout,
  E-Mail-/Reset-Tokens, Security-Stamp und seit .NET 10 **Passkeys (WebAuthn)** inklusive passwortloser Accounts
  und mehrerer Passkeys pro Account mit – keine Fremdbibliothek für WebAuthn.
- **Cookie-Anmeldung** (HttpOnly, Secure, SameSite=Lax, 30 Tage fest, nicht gleitend). Der WebSocket `/ws` nutzt
  dasselbe Cookie und prüft zusätzlich den `Origin`-Header (Schutz gegen Cross-Site-WebSocket-Hijacking).
- **Account-Seiten als Razor Pages** (serverseitig, Formulare mit Antiforgery). Für Passkeys rendert die Seite die
  WebAuthn-Optionen direkt ins HTML; ein kleines TS-Modul ruft `navigator.credentials` auf und schickt das Ergebnis
  per Formular zurück. Dadurch entsteht **keine JSON-API** und es braucht keine OpenAPI-Verträge.
- **Mail per SMTP** über ein eigenes Netcup-Postfach nur fürs Spiel (MailKit). Zugangsdaten nur in der `.env` des
  Deployments. In der Entwicklung werden Mails ins Log geschrieben.
- Passwortregeln nach NIST: mindestens 10 Zeichen, keine Zwangs-Sonderzeichen.
- Schlüssel für Cookies/Tokens (Data Protection) liegen in der Datenbank, damit Anmeldungen Neustarts und das
  read-only-Dateisystem des Containers überstehen.

## Konsequenzen

- Passkeys brauchen HTTPS und eine feste Domain (`Passkeys__ServerDomain`); lokal funktioniert `localhost`.
- Hinter nginx-proxy werden `X-Forwarded-*`-Header ausgewertet (Schema, Client-IP für Rate-Limiting). Der Container
  ist deshalb nur über das Proxy-Netz erreichbar zu betreiben, nicht mit veröffentlichtem Port.
- Passkey-Abläufe lassen sich nur eingeschränkt automatisch testen (kein echter Authenticator in der CI) → dokumentierte
  manuelle Prüfung.
