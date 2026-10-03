# Plan: Udgiftsdeling for 3 familier (+ ekstra person)

## 1. Formål

En web-app hvor tre familier (med børn) – og evt. én ekstra person – kan registrere fælles udgifter
(fx på en fælles ferie) og til sidst få beregnet **hvem der skal betale hvad til hvem**.

Ved afregning vægtes deltagerne:

| Deltagertype | Vægt |
|--------------|------|
| Voksen       | 1,0  |
| Barn         | 0,5  |

## 2. Antagelser

> Repositoriet er tomt i dag, så der findes ingen eksisterende kode eller dokumenterede designprincipper.
> "Nuværende designprincipper" tolkes derfor som gældende best practice for .NET/Blazor (se afsnit 4).
> Hvis der findes en intern guideline et andet sted, tilpasses planen til den.

- Der er præcis **3 familier** fra start; hver familie har 1..n voksne og 0..n børn.
- Der kan tilføjes **én ekstra person** (voksen, vægt 1,0), som afregnes som sin egen "husstand".
  Datamodellen understøtter dog N husstande, så grænsen kun er en forretningsregel og nem at ændre.
- Afregning sker **mellem husstande** (familier + ekstra person), ikke mellem enkeltpersoner.
- Valuta: DKK. Beløb håndteres som `decimal` og afrundes til øre.
- Én "begivenhed" (fx "Sommerhus 2026") samler husstande, deltagere og udgifter.

## 3. Funktionelle krav

### MVP
1. **Opret begivenhed** med navn og valuta.
2. **Administrer husstande**: 3 familier + mulighed for at tilføje/fjerne en ekstra person.
3. **Administrer deltagere** pr. familie: navn + type (Voksen/Barn).
4. **Registrer udgift**: beskrivelse, beløb, dato, betalt af (husstand), evt. kategori.
   - Som standard deles udgiften mellem **alle** deltagere.
   - (Valgfrit i MVP, men modellen skal understøtte det) vælg et udsnit af deltagere – fx hvis kun to familier var med til en restaurant.
5. **Rediger / slet udgift**.
6. **Afregningsoversigt**:
   - Samlet forbrug, samlet vægt, pris pr. vægtenhed.
   - Pr. husstand: betalt, andel, saldo (+ skal have / − skal betale).
   - **Minimal liste af overførsler** ("Familie B betaler 550 kr. til Familie C").
7. **Markér afregning som betalt** (lås begivenheden).

### Senere (ikke MVP)
- Login/brugere og deling via link.
- Kvitteringsbilleder.
- Eksport til PDF/CSV, MobilePay-links.
- Flere valutaer.

## 4. Teknisk arkitektur

### Stak
- **.NET 10 (LTS)**, C# 14, nullable reference types slået til, `TreatWarningsAsErrors`.
- **Blazor Web App** med *Interactive Server* render mode (enkel hosting, ingen API-lag nødvendigt i MVP).
  Komponenterne holdes render-mode-agnostiske, så skift til WebAssembly senere er muligt.
- **EF Core 10 + SQLite** (fil-database, nul opsætning). Kan skiftes til PostgreSQL/SQL Server via konfiguration.
- UI: Blazor-komponenter + **Bootstrap 5** (følger med skabelonen) eller **MudBlazor** – se åbne spørgsmål.
- Test: **xUnit**, **FluentAssertions/Shouldly**, **bUnit** til komponenttest.

### Designprincipper
- **Clean Architecture**: afhængigheder peger indad – Domain kender intet til EF eller Blazor.
- **SOLID** og **rig domænemodel**: forretningsregler (vægte, validering, max én ekstra person) ligger i domænet, ikke i UI.
- **Ren, testbar afregningslogik**: `SettlementCalculator` er en ren funktion uden I/O og er 100 % unit-testet.
- **Value objects** for `Money` og `Weight` – ingen løse `decimal`s gennem hele koden.
- **Dependency Injection** overalt; ingen statiske services.
- **Små, genbrugelige komponenter**; sider er tynde og kalder application services.
- Formatering via `.editorconfig`, `dotnet format` i CI.

### Løsningsstruktur
```
FamilyExpenses.sln
├── src/
│   ├── FamilyExpenses.Domain/          # Entiteter, value objects, SettlementCalculator
│   ├── FamilyExpenses.Application/     # Use cases/services, DTO'er, interfaces (IEventRepository)
│   ├── FamilyExpenses.Infrastructure/  # EF Core DbContext, migrations, repositories
│   └── FamilyExpenses.Web/             # Blazor Web App (sider, komponenter, DI-opsætning)
└── tests/
    ├── FamilyExpenses.Domain.Tests/
    ├── FamilyExpenses.Application.Tests/
    └── FamilyExpenses.Web.Tests/       # bUnit
```

## 5. Domænemodel

```csharp
public enum ParticipantType { Adult, Child }

public sealed class Event            // aggregate root
{
    Guid Id; string Name; string Currency; bool IsSettled;
    IReadOnlyList<Household> Households;
    IReadOnlyList<Expense> Expenses;

    Household AddFamily(string name);          // max 3 familier
    Household AddExtraPerson(string name);     // max 1, vægt 1,0
    Expense AddExpense(...);                   // validerer beløb > 0, betaler findes osv.
}

public enum HouseholdKind { Family, ExtraPerson }

public sealed class Household
{
    Guid Id; string Name; HouseholdKind Kind;
    IReadOnlyList<Participant> Participants;
    Weight TotalWeight => Participants.Sum(p => p.Weight);
}

public sealed class Participant
{
    Guid Id; string Name; ParticipantType Type;
    Weight Weight => Type == ParticipantType.Adult ? Weight.Adult : Weight.Child; // 1,0 / 0,5
}

public sealed class Expense
{
    Guid Id; string Description; Money Amount; DateOnly Date;
    Guid PaidByHouseholdId;
    IReadOnlyList<Guid> ParticipantIds;  // tom = alle deltagere
}
```

Vægtene ligger som konstanter i `Weight` (ét sted), så de nemt kan gøres konfigurerbare senere.

## 6. Afregningsalgoritme

For hver udgift:
1. Find de deltagende personer (alle, hvis intet er valgt).
2. `vægtsum = Σ vægt(deltager)`
3. Hver husstands andel = `beløb × (husstandens deltagervægt / vægtsum)`.
4. Afrund til øre; **resterende øre** fordeles deterministisk (største rest først), så summen altid går op.

Derefter pr. husstand:
- `saldo = betalt − andel` (positiv = skal have penge, negativ = skal betale).

Overførsler (minimer antal):
- Grådig algoritme: match største skyldner med største kreditor, overfør `min(|gæld|, tilgodehavende)`, gentag.
  Giver højst `husstande − 1` overførsler (her max 3).

### Regneeksempel

| Husstand | Voksne | Børn | Vægt | Betalt | Andel (1.000 kr./enhed) | Saldo |
|----------|--------|------|------|--------|-------------------------|-------|
| Familie A | 2 | 2 | 3,0 | 4.250 | 3.000 | **+1.250** |
| Familie B | 2 | 1 | 2,5 | 1.700 | 2.500 | **−800** |
| Familie C | 1 | 2 | 2,0 | 2.550 | 2.000 | **+550** |
| Ekstra person | 1 | 0 | 1,0 | 0 | 1.000 | **−1.000** |
| **I alt** | | | **8,5** | **8.500** | **8.500** | **0** |

Overførsler:
1. Ekstra person → Familie A: 1.000 kr.
2. Familie B → Familie A: 250 kr.
3. Familie B → Familie C: 550 kr.

Dette eksempel bliver den første acceptance-test.

## 7. UI / sider

| Route | Indhold |
|-------|---------|
| `/` | Liste over begivenheder + "Opret ny" |
| `/events/{id}` | Dashboard: husstande, samlet forbrug, seneste udgifter |
| `/events/{id}/households` | Familier og deltagere (voksen/barn-toggle), "Tilføj ekstra person" |
| `/events/{id}/expenses` | Udgiftsliste + formular (tilføj/rediger) |
| `/events/{id}/settlement` | Saldi-tabel + overførselsliste + "Markér som afregnet" |

Genbrugelige komponenter: `HouseholdCard`, `ParticipantEditor`, `ExpenseForm` (med `EditForm` + DataAnnotations/FluentValidation),
`MoneyDisplay` (formaterer med `da-DK`), `SettlementTable`, `TransferList`.

Mobil først (responsivt), da udgifter typisk registreres på telefonen. UI-tekster på dansk via `IStringLocalizer` (forberedt til flere sprog).

## 8. Teststrategi

- **Domain (højeste prioritet)**: `SettlementCalculator`
  - Regneeksemplet ovenfor.
  - Kun voksne → lige deling.
  - Udgift med udsnit af deltagere.
  - Øre-afrunding (fx 100 kr. delt på vægt 3,0) – summen skal gå op.
  - Alle har betalt præcis deres andel → ingen overførsler.
  - Ekstra person tilføjet/fjernet.
- **Domain-regler**: max 3 familier, max 1 ekstra person, beløb > 0, ingen ændringer efter afregning.
- **Application**: services mod in-memory SQLite.
- **Web**: bUnit-tests af `ExpenseForm` og `SettlementTable`.
- CI: GitHub Actions – `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.

## 9. Faser og leverancer

| Fase | Indhold | Færdig når |
|------|---------|------------|
| 0. Fundament | Solution, projekter, `.editorconfig`, `Directory.Build.props`, CI-workflow | Tom løsning bygger grønt i CI |
| 1. Domæne | Entiteter, value objects, `SettlementCalculator` + tests | Alle afregningstests grønne |
| 2. Persistens | EF Core, DbContext, migrations, repositories | Data overlever genstart |
| 3. Application | Services/use cases (opret begivenhed, husstande, udgifter, afregning) | Service-tests grønne |
| 4. UI – basis | Sider for begivenhed, husstande og udgifter | Man kan oprette alt via UI |
| 5. UI – afregning | Afregningsside, overførselsliste, lås begivenhed | Regneeksemplet kan gennemføres i browseren |
| 6. Polish | Mobil-layout, validering, fejlhåndtering, bUnit-tests | Klar til brug |

## 10. Åbne spørgsmål

1. **Ekstra person**: altid voksen (vægt 1,0), eller kan det også være et barn? Kun én, eller "én ad gangen"?
2. **Udsnit af deltagere** pr. udgift – nødvendigt i MVP, eller deles alt altid mellem alle?
3. **Betaler** – er det altid en husstand, eller skal man kunne se hvilken person der lagde ud?
4. **Login** – skal familierne kunne logge ind hver for sig, eller er det én delt app/link?
5. **UI-bibliotek** – Bootstrap (standard skabelon) eller MudBlazor (Material-design)?
6. **Hosting** – lokalt, Azure App Service, eller Docker?
