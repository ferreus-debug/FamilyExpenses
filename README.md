# FamilyExpenses

Udgiftsdeling for tre familier (+ en ekstra person), bygget i C# og Blazor.
Voksne tæller 1 og børn 0,5, når udgifterne afregnes. Se [docs/PLAN.md](docs/PLAN.md) for den fulde plan.

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

CI bygger en selvstændig `linux-arm64`-udgave (artefakt `familyexpenses-linux-arm64`), som kan køre på en
Raspberry Pi 4 med 64-bit OS uden at .NET er installeret. Drift sker via systemd og Cloudflare Tunnel – se planen, afsnit 8a.
