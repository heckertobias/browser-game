# ADR 0007 – Bootstrap fürs Admin-Interface

**Status:** akzeptiert (ergänzt [ADR 0001](0001-tech-stack.md))

## Kontext

ADR 0001 legt daisyUI/Tailwind als gemeinsames UI-Framework für Spiel-UI und Razor-Seiten fest. Das Admin-Interface
(004, eigener Dienst nach ADR 0006) hat es übernommen, im Pixel-Akzent-Stil des Spiels. Im Betrieb zeigt sich: Im
Admin zählen Übersicht und Funktion, nicht die Spieloptik (005). Je mehr Bereiche dazukommen, desto mehr braucht es
fertige Bausteine für Verwaltungsoberflächen (Seitenleiste, Tabellen, Formular-Validierung, Hell/Dunkel).

## Entscheidung

- Das Admin-Interface nutzt **Bootstrap 5.3** und **Bootstrap Icons**, lokal ausgeliefert (kein CDN), per npm in
  `client/` gepinnt und von Dependabot gepflegt.
- Spiel-Client und Startseite bleiben bei daisyUI/Tailwind (ADR 0001 gilt dort weiter).
- Kein Tailwind im Admin: Bootstrap bringt Layout-Hilfen mit; Eigenes beschränkt sich auf eine kleine CSS-Datei.

## Alternativen

- **daisyUI mit neutralem Theme:** kein neues Framework, aber weniger fertige Admin-Bausteine (Validierung,
  Offcanvas-Navigation) und weiterhin dieselbe Optik-Familie wie das Spiel.
- **Tabler** (Admin-Template auf Bootstrap): mehr fertige Dashboard-Optik, aber größere zusätzliche Abhängigkeit für
  wenig Mehrwert bei einer Handvoll Seiten.
- **Material (MDB, Material Web):** für serverseitige Razor-Seiten umständlicher (Web Components bzw. JS nötig).

## Folgen

- Zwei Styling-Welten im Repo: Razor-Admin (Bootstrap) und Spiel/Startseite (daisyUI). Sie teilen nichts, das ist
  durch die getrennten Dienste ohnehin so.
- Validierungsklassen von ASP.NET Core (`input-validation-error`) und Bootstrap (`is-invalid`) unterscheiden sich;
  ein kleiner TagHelper gleicht das aus.
