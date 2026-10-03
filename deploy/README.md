# Drift på Raspberry Pi

Appen kører som en selvstændig `linux-arm64`-app (ingen .NET-installation på Pi'en) under systemd og
nås udefra via **Cloudflare Tunnel** – på samme måde som `budget.mathiasspangsberg.com` og
`madplan.mathiasspangsberg.com`. Adresse: **`turkassen.mathiasspangsberg.com`**.

```
Telefon ──https──▶ Cloudflare ──tunnel──▶ cloudflared (Pi) ──http──▶ 127.0.0.1:5080 (appen)
```

## Krav

- Raspberry Pi 4 med **64-bit** Raspberry Pi OS: `uname -m` skal give `aarch64`.
- SSH-adgang til Pi'en med en bruger, der har `sudo`.
- `cloudflared` kører allerede på Pi'en (som for de andre apps).
- På din egen maskine: .NET 10 SDK, `rsync` og `ssh` (Linux, macOS eller WSL på Windows).

## 1. Installér / opdatér appen

Fra roden af repoet på din egen maskine:

```bash
deploy/publish-pi.sh pi@raspberrypi.local          # standardport 5080
deploy/publish-pi.sh pi@raspberrypi.local 5081     # hvis 5080 er optaget
```

Scriptet bygger appen, kopierer den til Pi'en og kører `install-pi.sh`, som:

- tjekker at Pi'en er 64-bit, og at porten ikke er optaget af en anden app,
- opretter systembrugeren `familyexpenses` og installerer appen i `/opt/familyexpenses`,
- installerer `familyexpenses.service` (starter ved boot, genstarter ved fejl, hærdet sandbox),
- installerer daglig backup (`familyexpenses-backup.timer`, kl. 03:30),
- starter appen og tjekker `http://127.0.0.1:<port>/healthz`.

Kør samme kommando igen for hver ny version. Databasen og indstillingerne bevares.

> Uden `rsync`/WSL: hent artefaktet `familyexpenses-linux-arm64` fra seneste CI-kørsel på GitHub, pak det ud
> på Pi'en som `~/familyexpenses-release/app`, kopiér `deploy/` til `~/familyexpenses-release/deploy`
> og kør `sudo ~/familyexpenses-release/deploy/install-pi.sh`.

## 2. Cloudflare Tunnel

Tilføj en regel i den **samme tunnel** som budget og madplan. Brug den måde, de andre apps er sat op på:

**Tunnel styret fra Cloudflare-dashboardet** (oftest):

1. dash.cloudflare.com → **Zero Trust** → **Networks** → **Tunnels** → vælg tunnellen, som budget/madplan bruger → **Configure**.
2. Fanen **Public Hostname** (i nyere UI: **Published application routes**) → **Add a public hostname**.
3. Subdomain: `turkassen` · Domain: `mathiasspangsberg.com` · Path: tom
4. Service: Type **HTTP**, URL **`127.0.0.1:5080`** (brug `127.0.0.1`, ikke `localhost` – appen lytter kun på IPv4).
5. Gem. DNS-recorden oprettes automatisk.

Se samtidig, hvilke porte budget og madplan bruger i listen – de må ikke være 5080.

**Tunnel styret med `config.yml` på Pi'en:** se [`cloudflared-ingress.example.yml`](cloudflared-ingress.example.yml), og kør derefter
`cloudflared tunnel route dns <tunnel-navn> turkassen.mathiasspangsberg.com` og `sudo systemctl restart cloudflared`.

Tjek bagefter: `https://turkassen.mathiasspangsberg.com/healthz` skal vise `Healthy`.
Under SSL/TLS → Edge Certificates bør **Always Use HTTPS** være slået til (som for domænets andre apps).

## 3. Første bruger

Åbn `https://turkassen.mathiasspangsberg.com/Account/Register` og opret dig. **Den første bruger bliver ejer**.
Derefter kan nye konti kun oprettes via invitationslinks fra appen.

## 4. Automatisk deploy

Når en pull request merges til `main`, kører CI. Er bygget grønt, installerer workflowet
[`deploy.yml`](../.github/workflows/deploy.yml) den samme `linux-arm64`-build på Pi'en. Det sker gennem en
GitHub Actions-runner, der kører på Pi'en og selv forbinder ud til GitHub, så intet nyt eksponeres.
Pull requests deployes aldrig, og `main` er beskyttet: ændringer skal ind via en pull request med grøn CI.

Opsætning (én gang) – på Pi'en, fra en kopi af repoet:

1. GitHub → repoet → **Settings** → **Actions** → **Runners** → **New self-hosted runner**, og kopiér tokenet
   fra `config.sh`-linjen (gælder i 1 time).
2. `sudo deploy/setup-runner.sh <token>`

Scriptet opretter brugeren `github-runner`, installerer runneren som systemd-tjeneste (label `familyexpenses`)
og giver den lov til at køre præcis ét script med sudo: `/usr/local/sbin/familyexpenses-deploy`.
Bruger appen en anden port end 5080, så sæt repo-variablen `PI_PORT` (Settings → Secrets and variables →
Actions → Variables).

Deploys kan følges under **Actions** → **Deploy**, og miljøet **production** viser, hvilken commit der kører.
Svarer appen ikke på `/healthz` efter installationen, fejler deployet – se `journalctl -u familyexpenses -n 50`
og ret fejlen med en ny pull request (eller revert den seneste).

> Runneren kører kun kode, der er merget til `main`, men den kan installere hvad som helst som root via
> deploy-scriptet. Giv derfor kun skriveadgang til repoet til folk, du stoler på.

## Drift

| Opgave | Kommando |
|--------|----------|
| Status / log | `systemctl status familyexpenses` · `journalctl -u familyexpenses -f` |
| Genstart | `sudo systemctl restart familyexpenses` |
| Indstillinger | `sudo nano /etc/familyexpenses/familyexpenses.env` (port, backup-mappe, Docker-netværk) |
| Kør backup nu | `sudo systemctl start familyexpenses-backup` |
| Se backups | `sudo ls -lh /var/lib/familyexpenses/backups` |

### Gendan fra backup

```bash
sudo systemctl stop familyexpenses
sudo -u familyexpenses sh -c 'cd /var/lib/familyexpenses && rm -f familyexpenses.db-wal familyexpenses.db-shm \
  && gunzip -c backups/familyexpenses-<dato>.db.gz > familyexpenses.db'
sudo systemctl start familyexpenses
```

Backups ligger som standard på SD-kortet. Sæt `BACKUP_DIR` i env-filen til fx en USB-disk eller NAS,
så de overlever et dødt SD-kort.

## Fejlfinding

- **502 / "Bad gateway" fra Cloudflare:** appen kører ikke, eller tunnel-reglen peger på en forkert port.
  Tjek `curl http://127.0.0.1:5080/healthz` på Pi'en.
- **Login virker ikke / man bliver logget ud:** tjek at tunnelen sender `X-Forwarded-Proto` (det gør cloudflared som standard).
  Kører cloudflared i Docker, så sæt `ReverseProxy__KnownNetworks__0=172.17.0.0/16` i env-filen.
- **Invitationslinks starter med `http://`:** samme årsag som ovenfor.
