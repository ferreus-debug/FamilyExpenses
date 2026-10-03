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
- Der kan tilføjes **én ekstra person**, som afregnes som sin egen "husstand". Man vælger selv navn og
  om personen er **voksen (1,0) eller barn (0,5)**. Den ekstra person kan udskiftes/fjernes.
  Datamodellen understøtter N husstande, så grænsen kun er en forretningsregel og nem at ændre.
- Afregning sker **mellem husstande** (familier + ekstra person), men det registreres **hvilken person** der lagde ud.
- Hver familie har sit eget **login**; en bruger tilknyttes én husstand.
- Appen hostes på brugerens egen **Raspberry Pi**.
- Valuta: DKK. Beløb håndteres som `decimal` og afrundes til øre.
- Én "begivenhed" (fx "Sommerhus 2026") samler husstande, deltagere og udgifter.

## 3. Funktionelle krav

### MVP
1. **Opret begivenhed** med navn og valuta.
2. **Administrer husstande**: 3 familier + mulighed for at tilføje/fjerne/udskifte en ekstra person
   (navn + voksen/barn vælges).
3. **Administrer deltagere** pr. familie: navn + type (Voksen/Barn).
4. **Registrer udgift**: beskrivelse, beløb, dato, **betalt af (person)**, evt. kategori.
   - Betaler vælges som en konkret deltager; afregningen krediterer personens husstand.
   - Som standard deles udgiften mellem **alle** deltagere.
   - Man kan vælge et **udsnit af deltagere** (pr. person, med "vælg hele familien"-genvej) – fx hvis kun to familier var med til en restaurant.
   - Udgiftslisten viser hvem der lagde ud, og hvem udgiften deles mellem.
5. **Rediger / slet udgift**.
6. **Afregningsoversigt**:
   - Samlet forbrug, samlet vægt, pris pr. vægtenhed.
   - Pr. husstand: betalt, andel, saldo (+ skal have / − skal betale).
   - **Minimal liste af overførsler** ("Familie B betaler 550 kr. til Familie C").
7. **Markér afregning som betalt** (lås begivenheden).
8. **Login og adgang**:
   - Hver familie har én eller flere brugerkonti, som er tilknyttet familiens husstand.
   - Den der opretter begivenheden er **administrator** og inviterer familierne via et invitationslink
     (engangstoken). Brugeren opretter konto via linket og kobles automatisk til husstanden.
   - Alle indloggede i begivenheden kan se alle udgifter og afregningen.
   - Man kan oprette og redigere udgifter; man kan kun redigere/slette **egne** udgifter (administrator kan alle).
   - Den ekstra person har ikke nødvendigvis login – administrator eller en familie registrerer på personens vegne.

### Senere (ikke MVP)
- Kvitteringsbilleder.
- Eksport til PDF/CSV, MobilePay-links.
- Flere valutaer.

## 4. Teknisk arkitektur

### Stak
- **.NET 10 (LTS)**, C# 14, nullable reference types slået til, `TreatWarningsAsErrors`.
- **Blazor Web App** med *Interactive Server* render mode (enkel hosting, ingen API-lag nødvendigt i MVP,
  lavt ressourceforbrug på klienten). Komponenterne holdes render-mode-agnostiske.
- **EF Core 10 + SQLite** (fil-database, nul opsætning – passer godt til en Raspberry Pi).
- **ASP.NET Core Identity** (cookie-login) med EF Core-store i samme SQLite-database.
  Login-siderne er statisk server-renderet (som i Blazor-skabelonen), resten interaktivt.
- UI: **MudBlazor** (Material Design) – `MudDataGrid`, `MudDialog`, `MudForm`, `MudSnackbar`, dansk kultur `da-DK`.
- Test: **xUnit**, **FluentAssertions/Shouldly**, **bUnit** til komponenttest.

### Designprincipper
- **Clean Architecture**: afhængigheder peger indad – Domain kender intet til EF eller Blazor.
- **SOLID** og **rig domænemodel**: forretningsregler (vægte, validering, max én ekstra person) ligger i domænet, ikke i UI.
- **Autorisation i Application-laget** (ikke kun skjulte knapper): hver service-kald tjekker at brugeren
  tilhører begivenheden, og at redigering kun sker på egne udgifter. Bruges via `ICurrentUser`-abstraktion.
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
│   ├── FamilyExpenses.Infrastructure/  # EF Core DbContext, Identity, migrations, repositories
│   └── FamilyExpenses.Web/             # Blazor Web App + MudBlazor (sider, komponenter, DI-opsætning)
├── deploy/
│   ├── familyexpenses.service          # systemd-unit til Raspberry Pi
│   ├── Caddyfile                       # reverse proxy + HTTPS
│   └── publish-pi.sh                   # build linux-arm64 + kopiér til Pi
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

    Household AddFamily(string name);                             // max 3 familier
    Household SetExtraPerson(string name, ParticipantType type);  // max 1, voksen eller barn; erstatter eksisterende
    void RemoveExtraPerson();                                     // kun hvis personen ikke indgår i udgifter
    Expense AddExpense(...);                                      // validerer beløb > 0, betaler og deltagere findes osv.
}

public enum HouseholdKind { Family, ExtraPerson }

public sealed class Household
{
    Guid Id; string Name; HouseholdKind Kind;
    IReadOnlyList<Participant> Participants;   // ExtraPerson har præcis én deltager
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
    Guid PaidByParticipantId;            // konkret person; husstand udledes heraf
    IReadOnlyList<Guid> ParticipantIds;  // tom = alle deltagere
    string CreatedByUserId;              // til "kun egne udgifter"-reglen
}

// Infrastructure (Identity) – ikke en del af domænet
public sealed class AppUser : IdentityUser
{
    Guid? HouseholdId;                   // brugeren tilhører én husstand
}

public sealed class Invitation
{
    Guid Id; Guid EventId; Guid HouseholdId; string TokenHash; DateTime ExpiresUtc; bool Used;
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
- `betalt = Σ udgifter hvor betaleren er en person i husstanden`
- `saldo = betalt − andel` (positiv = skal have penge, negativ = skal betale).
- Afregningssiden kan desuden vise betalt pr. person (informativt), men overførsler sker mellem husstande.

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
| `/Account/Login`, `/Account/Register?invite=…` | Login / opret konto via invitation (Identity) |
| `/` | Mine begivenheder + "Opret ny" |
| `/events/{id}/invitations` | (Admin) Generér invitationslink pr. familie |
| `/events/{id}` | Dashboard: husstande, samlet forbrug, seneste udgifter |
| `/events/{id}/households` | Familier og deltagere (voksen/barn-toggle), "Tilføj/udskift ekstra person" (navn + voksen/barn) |
| `/events/{id}/expenses` | `MudDataGrid` med udgifter (betalt af person, deles mellem) + dialog til tilføj/rediger |
| `/events/{id}/settlement` | Saldi-tabel + overførselsliste + "Markér som afregnet" |

Genbrugelige komponenter (MudBlazor):
- `HouseholdCard` (`MudCard`), `ParticipantEditor` (`MudChipSet` voksen/barn),
- `ExpenseDialog` (`MudDialog` + `MudForm` + FluentValidation): beløb, betaler (`MudSelect` grupperet pr. familie),
  deltagere (`MudTreeView`/checkbokse pr. familie med "vælg alle"),
- `MoneyDisplay` (formaterer med `da-DK`), `SettlementTable` (`MudSimpleTable`), `TransferList` (`MudList`).

Layout: `MudLayout` med `MudAppBar` + `MudDrawer` (kollapser på mobil), eget `MudTheme` (lys/mørk).
Mobil først, da udgifter typisk registreres på telefonen. UI-tekster på dansk via `IStringLocalizer`.

## 8. Teststrategi

- **Domain (højeste prioritet)**: `SettlementCalculator`
  - Regneeksemplet ovenfor.
  - Kun voksne → lige deling.
  - Udgift med udsnit af deltagere.
  - Øre-afrunding (fx 100 kr. delt på vægt 3,0) – summen skal gå op.
  - Alle har betalt præcis deres andel → ingen overførsler.
  - Ekstra person som voksen vs. barn; tilføjet/fjernet.
  - Betaler er en person, der ikke selv deltager i udgiften (skal stadig krediteres fuldt).
- **Domain-regler**: max 3 familier, max 1 ekstra person, beløb > 0, betaler/deltagere skal tilhøre begivenheden,
  ingen ændringer efter afregning.
- **Application**: services mod in-memory SQLite, inkl. autorisation (bruger fra anden familie kan ikke redigere andres udgift,
  bruger uden for begivenheden får afvist adgang).
- **Integration**: invitationsflow (token udløber, kan kun bruges én gang).
- **Web**: bUnit-tests af `ExpenseForm` og `SettlementTable`.
- CI: GitHub Actions – `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`,
  samt `dotnet publish -r linux-arm64` så Pi-buildet altid er verificeret.

## 8a. Drift på Raspberry Pi

- **Krav**: Raspberry Pi 4/5 med 64-bit Raspberry Pi OS (arm64). .NET understøtter ikke 32-bit ARM-OS fremover, så 64-bit er et must.
- **Build**: `dotnet publish src/FamilyExpenses.Web -c Release -r linux-arm64 --self-contained`
  → ingen .NET-installation nødvendig på Pi'en. `deploy/publish-pi.sh` bygger og kopierer via `rsync`/`scp`.
- **Kørsel**: systemd-service (`deploy/familyexpenses.service`) med `Restart=always`, egen bruger,
  `ASPNETCORE_URLS=http://127.0.0.1:5000`, data i `/var/lib/familyexpenses/app.db`.
- **Reverse proxy + HTTPS**: **Caddy** (automatisk Let's Encrypt-certifikat) foran Kestrel. Blazor Server kræver WebSockets – virker ud af boksen i Caddy.
- **Adgang udefra** (så familierne kan bruge den på telefonen): enten
  a) port-forward 443 + DynDNS/eget domæne, eller
  b) **Tailscale**/Cloudflare Tunnel uden åbne porte (anbefales – nemmest og sikrest).
- **Data Protection-nøgler** persisteres til disk (`PersistKeysToFileSystem`), så logins overlever genstart.
- **Migrations** køres automatisk ved opstart (`Database.Migrate()`) – acceptabelt for én instans.
- **Backup**: cron-job med `sqlite3 app.db ".backup ..."` dagligt til USB/NAS.
- **Alternativ**: Docker-image (`mcr.microsoft.com/dotnet/aspnet:10.0` har arm64-variant), hvis Docker allerede kører på Pi'en.

## 9. Faser og leverancer

| Fase | Indhold | Færdig når |
|------|---------|------------|
| 0. Fundament | Solution, projekter, `.editorconfig`, `Directory.Build.props`, CI-workflow | Tom løsning bygger grønt i CI |
| 1. Domæne | Entiteter, value objects, `SettlementCalculator` + tests | Alle afregningstests grønne |
| 2. Persistens + Identity | EF Core, DbContext, Identity, migrations, repositories | Data og brugere overlever genstart |
| 3. Application | Services/use cases (begivenhed, husstande, udgifter, afregning, invitationer) + autorisation | Service-tests grønne |
| 4. UI – basis | MudBlazor-layout, login, invitationsflow, sider for husstande og udgifter | Familier kan logge ind og oprette udgifter |
| 5. UI – afregning | Afregningsside, overførselsliste, lås begivenhed | Regneeksemplet kan gennemføres i browseren |
| 6. Raspberry Pi | Publish-script, systemd, Caddy, backup, fjernadgang | Appen kører på Pi'en og kan nås fra telefonen |
| 7. Polish | Mobil-layout, validering, fejlhåndtering, bUnit-tests | Klar til brug |

## 10. Beslutninger

| # | Spørgsmål | Beslutning |
|---|-----------|------------|
| 1 | Ekstra person | Man vælger navn og om det er en voksen (1,0) eller et barn (0,5) |
| 2 | Udsnit af deltagere pr. udgift | Ja, med i MVP |
| 3 | Betaler | En konkret person registreres; afregning sker pr. husstand |
| 4 | Login | Ja – hver familie har login, brugere tilknyttes en husstand via invitation |
| 5 | UI-bibliotek | MudBlazor |
| 6 | Hosting | Brugerens Raspberry Pi (linux-arm64, systemd + Caddy) |

## 11. Resterende åbne spørgsmål

1. Fjernadgang til Pi'en: Tailscale/Cloudflare Tunnel eller port-forward med eget domæne?
2. Hvilken Pi-model og OS (skal være 64-bit)?
3. Skal en familie kunne have flere logins (fx begge forældre), eller én fælles konto pr. familie? (Planen understøtter flere.)
