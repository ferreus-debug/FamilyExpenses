# FamilyExpenses

Udgiftsdeling for familier og venner på tur (op til 12 familier/husstande + en ekstra person), bygget i C# og Blazor.
Voksne tæller 1, børn 0,5 og babyer 0, når udgifterne afregnes. Se [docs/PLAN.md](docs/PLAN.md) for den fulde plan.

Hver udgift kan have op til 5 billeder (fx kvitteringen). Telefonen skalerer dem ned til højst 1600 px JPEG før
upload (og fjerner dermed GPS-data), og de gemmes i databasen, så backup og rollback dækker dem. Kun medlemmer af
begivenheden kan se dem (`/pictures/{id}`).

"Udfyld fra kvittering" læser et billede af kvitteringen med Claude og udfylder beskrivelse, beløb og dato
(beløb kun i kroner – andre valutaer skal man selv omregne). Funktionen er slået fra, indtil der er sat en
Anthropic API-nøgle i `Receipts:AnthropicApiKey` (miljøvariablen `Receipts__AnthropicApiKey`).

## Kom i gang

Kræver [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/FamilyExpenses.Web   # start appen
dotnet test                                   # kør tests
dotnet format                                 # formatér koden
```

Databasen er en SQLite-fil (`familyexpenses.db` som standard – kan ændres med `ConnectionStrings__Default`).
Migrations køres automatisk ved opstart. Ny migration efter ændringer i modellen:

```bash
dotnet tool restore
dotnet ef migrations add <Navn> --project src/FamilyExpenses.Infrastructure --output-dir Persistence/Migrations
```

## Første opstart og invitationer

1. Den **første** bruger logger ind med Google på `/Account/Register` og bliver ejer af appen (login sker via Firebase-projektet `turkassen`, se `Firebase` i `appsettings.json`).
2. Derefter kan nye konti **kun** oprettes via et invitationslink (Begivenhed → Invitationer → "Opret link").
3. Den inviterede åbner linket, opretter konto (eller logger ind) og bliver koblet til sin familie.

## End-to-end-test i browser

`tests/e2e/run.sh` starter appen på en midlertidig database og kører hele forløbet i Chromium
(registrering, familier, invitation, udgifter, adgangsregler, log ud/ind, mobilvisning). Kræver Node 20+.

```bash
CHROME_PATH=/usr/bin/chromium tests/e2e/run.sh   # skærmbilleder havner i tests/e2e/shots/
```

## Struktur

| Projekt | Ansvar |
|---------|--------|
| `src/FamilyExpenses.Domain` | Entiteter, value objects og afregningslogik – ingen afhængigheder |
| `src/FamilyExpenses.Application` | Use cases og interfaces |
| `src/FamilyExpenses.Infrastructure` | EF Core (SQLite) og Identity |
| `src/FamilyExpenses.Web` | Blazor Web App med MudBlazor |
| `tests/*` | xUnit + Shouldly, bUnit til komponenter |

Pakkeversioner styres centralt i `Directory.Packages.props`; fælles build-indstillinger i `Directory.Build.props`
(nullable, warnings som fejl, code style håndhæves i build).

## Raspberry Pi

Se **[deploy/README.md](deploy/README.md)** for installation, Cloudflare Tunnel, backup og fejlfinding.

CI bygger en selvstændig `linux-arm64`-udgave (artefakt `familyexpenses-linux-arm64`), som kan køre på en
Raspberry Pi 4 med 64-bit OS uden at .NET er installeret. Drift sker via systemd og Cloudflare Tunnel – se planen, afsnit 8a.
