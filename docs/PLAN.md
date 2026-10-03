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
   - Som standard deles udgiften mellem **alle** deltagere, der er med på oprettelsestidspunktet
     (gemmes eksplicit, så personer der tilføjes senere ikke ændrer tidligere udgifter).
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
   - **Registrering er lukket**: kun den allerførste bruger (ejeren) og personer med et gyldigt invitationslink
     kan oprette konto, så fremmede ikke kan oprette sig på den offentlige adresse.

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
│   ├── cloudflared-config.yml          # eksempel på ingress-regel til Cloudflare Tunnel
│   └── publish-pi.sh                   # build linux-arm64 + kopiér til Pi
└── tests/
    ├── FamilyExpenses.Domain.Tests/
    ├── FamilyExpenses.Application.Tests/
    └── FamilyExpenses.Web.Tests/       # bUnit
```

## 5. Domænemodel

```csharp
public enum ParticipantType { Adult, Child }

public sealed class ExpenseEvent     // aggregate root ("Event" er et reserveret ord i VB → CA1716)
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
    IReadOnlyList<Guid> SharedWithParticipantIds;  // altid eksplicit; "alle" udfyldes ved oprettelse
    string CreatedByUserId;              // til "kun egne udgifter"-reglen
}

// Adgang (Domain/Access) – separate aggregater, refererer events via id
public sealed class EventMember      // bruger ↔ begivenhed (+ husstand); unik pr. (EventId, UserId)
{
    Guid EventId; string UserId; Guid? HouseholdId; MemberRole Role;   // Admin | Member
}

public sealed class Invitation       // engangslink pr. husstand; kun SHA-256-hash af token gemmes
{
    Guid EventId; Guid HouseholdId; string TokenHash; DateTimeOffset ExpiresAt;
    string? AcceptedByUserId; DateTimeOffset? AcceptedAt;
    EventMember Accept(string userId, DateTimeOffset now);   // fejler hvis brugt eller udløbet
}

// Infrastructure (Identity)
public sealed class AppUser : IdentityUser { string DisplayName; }
```

Vægtene ligger som konstanter i `Weight` (ét sted), så de nemt kan gøres konfigurerbare senere.

En bruger kan være med i flere begivenheder (fx sommerferie og skiferie) og tilhøre én husstand i hver –
derfor ligger koblingen i `EventMember` i stedet for på `AppUser`.

### Persistens (fase 2)
- Én SQLite-fil indeholder domænedata, Identity-tabeller og Data Protection-nøgler (én fil at tage backup af).
- `Money` gemmes som heltal i øre (`AmountMinorUnits`), `SharedWithParticipantIds` som JSON-array.
- Husstande og deltagere har en persisteret `SortOrder`, så visning og øre-afrunding er identisk efter genindlæsning.
- Application kender kun `IUnitOfWorkFactory`/repositories; hver operation får sin egen kortlivede `DbContext`
  (anbefalet til Blazor Server, hvor et circuit lever længe).
- Migrations køres automatisk ved opstart.

## 6. Afregningsalgoritme

For hver udgift:
1. Find de deltagende personer (alle, hvis intet er valgt).
2. `vægtsum = Σ vægt(deltager)`
3. Hver husstands andel = `beløb × (husstandens deltagervægt / vægtsum)`.
4. Afrund til øre; **resterende øre** fordeles deterministisk (største rest først), så summen altid går op.
   Afrundingen sker **pr. udgift**, så saldi kan afvige med få øre fra en beregning på totalbeløbet.

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
2. Familie B → Familie C: 550 kr.
3. Familie B → Familie A: 250 kr.

Dette eksempel bliver den første acceptance-test.

### Application services og adgangsregler (fase 3)

| Service | Funktioner |
|---------|------------|
| `EventService` | opret (opretter bliver admin), mine begivenheder, detaljer, omdøb, afregn/genåbn, slet, admin vælger egen husstand |
| `HouseholdService` | familier, ekstra person, deltagere |
| `ExpenseService` | liste (med "deles mellem"-opsummering og `CanEdit`), opret, ret, slet |
| `SettlementService` | saldi, betalt pr. person, overførsler |
| `InvitationService` | opret link, liste med status, forhåndsvisning uden login, accepter |

| Handling | Admin | Familiemedlem | Andre |
|----------|:-----:|:-------------:|:-----:|
| Se begivenhed, udgifter, afregning | ✔ | ✔ | – (ser "findes ikke") |
| Opret udgift (også på andres vegne) | ✔ | ✔ | – |
| Ret/slet udgift | alle | egne | – |
| Deltagere i egen familie | ✔ | ✔ | – |
| Familier, ekstra person, invitationer, afregn, slet | ✔ | – | – |

Reglerne tjekkes ét sted (`EventAccess`) ved hvert service-kald. Webben leverer den indloggede bruger via
`ICurrentUser` (en `CircuitHandler` holder brugeren opdateret i Blazor-circuits).

## 7. UI / sider

| Route | Indhold |
|-------|---------|
| `/Account/Login`, `/Account/Register?invite=…` | Login / opret konto via invitation (Identity) |
| `/` | Mine begivenheder + "Opret ny" |
| `/events/{id}/invitations` | (Admin) Generér invitationslink pr. familie |
| `/events/{id}` | Dashboard: husstande, samlet forbrug, seneste udgifter |
| `/events/{id}/households` | Familier og deltagere (voksen/barn-toggle), "Tilføj/udskift ekstra person" (navn + voksen/barn) |
| `/events/{id}/expenses` | `MudDataGrid` med udgifter (betalt af person, deles mellem) + dialog til tilføj/rediger |
| `/events/{id}/settlement` | "Hvem betaler hvem" (egen husstand fremhævet), saldi-tabel med betalt pr. person, "Kopiér som tekst" til SMS, "Markér som afregnet"/"Genåbn" (admin) |

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

## 8a. Drift på Raspberry Pi (fase 6 – se `deploy/README.md`)

- **Adresse**: `udgifter.mathiasspangsberg.com` via samme Cloudflare Tunnel som `budget.` og `madplan.mathiasspangsberg.com`.
- **Hardware/OS**: Raspberry Pi 4 med 64-bit Raspberry Pi OS (`aarch64`). Appen bruger ca. 140 MB RAM.
- **Build**: selvstændig `linux-arm64`-udgave (ingen .NET på Pi'en). CI bygger den på hver commit.
- **Installation**: `deploy/publish-pi.sh bruger@pi [port]` bygger, kopierer og kører `deploy/install-pi.sh` på Pi'en
  (idempotent – samme kommando til opdateringer). Scriptet afviser 32-bit OS og en port, der er optaget af en anden app.
- **Kørsel**: systemd `Type=notify` (`UseSystemd()`), egen systembruger, kun skriveadgang til `/var/lib/familyexpenses`,
  Kestrel lytter kun på `http://127.0.0.1:5080`. Lokale indstillinger i `/etc/familyexpenses/familyexpenses.env`.
- **Bag tunnelen**: forwarded headers (`X-Forwarded-Proto/For`) fra loopback, så cookies er `Secure`, HSTS sendes og
  invitationslinks får `https://`. Ingen HTTPS-redirect i appen; Cloudflares "Always Use HTTPS" står for det.
  Er cloudflared i Docker: `ReverseProxy__KnownNetworks__0=172.17.0.0/16`.
- **Sundhedstjek**: `/healthz` (inkl. database).
- **Backup**: `familyexpenses-backup.timer` dagligt kl. 03:30 → `sqlite3 .backup` (konsistent mens appen kører) +
  integritetstjek + gzip, 30 dages rotation; `BACKUP_DIR` kan pege på USB/NAS.
- **Migrations** køres automatisk ved opstart; Data Protection-nøgler ligger i databasen, så logins overlever genstart.

## 9. Faser og leverancer

| Fase | Indhold | Færdig når |
|------|---------|------------|
| 0. Fundament | Solution, projekter, `.editorconfig`, `Directory.Build.props`, CI-workflow | Tom løsning bygger grønt i CI |
| 1. Domæne | Entiteter, value objects, `SettlementCalculator` + tests | Alle afregningstests grønne |
| 2. Persistens + Identity | EF Core, DbContext, Identity, migrations, repositories | Data og brugere overlever genstart |
| 3. Application | Services/use cases (begivenhed, husstande, udgifter, afregning, invitationer) + autorisation | Service-tests grønne |
| 4. UI – basis | MudBlazor-layout, login, invitationsflow, sider for husstande og udgifter | Familier kan logge ind og oprette udgifter |
| 5. UI – afregning | Afregningsside, overførselsliste, lås begivenhed | Regneeksemplet kan gennemføres i browseren |
| 6. Raspberry Pi | Publish-script, systemd, Cloudflare Tunnel-regel, backup | Appen kører på Pi'en og kan nås fra telefonen |
| 7. Polish | Mobil-layout, validering, fejlhåndtering, bUnit-tests | Klar til brug |

## 10. Beslutninger

| # | Spørgsmål | Beslutning |
|---|-----------|------------|
| 1 | Ekstra person | Man vælger navn og om det er en voksen (1,0) eller et barn (0,5) |
| 2 | Udsnit af deltagere pr. udgift | Ja, med i MVP |
| 3 | Betaler | En konkret person registreres; afregning sker pr. husstand |
| 4 | Login | Ja – hver familie har login, brugere tilknyttes en husstand via invitation |
| 5 | UI-bibliotek | MudBlazor |
| 6 | Hosting | Brugerens Raspberry Pi 4 (linux-arm64, systemd) |
| 7 | Fjernadgang | Cloudflare Tunnel, som de øvrige apps |

## 11. Resterende åbne spørgsmål

1. Bekræft at Pi'en kører 64-bit OS (`uname -m` → `aarch64`).
2. Hvilket (under)domæne skal appen have?
3. Skal en familie kunne have flere logins (fx begge forældre), eller én fælles konto pr. familie? (Planen understøtter flere.)
