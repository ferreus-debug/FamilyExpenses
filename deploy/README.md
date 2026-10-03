# Drift på Raspberry Pi

Appen kører som en selvstændig `linux-arm64`-app (ingen .NET-installation på Pi'en) under systemd og
nås udefra via **Cloudflare Tunnel** – på samme måde som `budget.mathiasspangsberg.com` og
`madplan.mathiasspangsberg.com`. Adresse: **`turkassen.mathiasspangsberg.com`**.

```
Telefon ──https──▶ Cloudflare ──tunnel──▶ cloudflared (Pi) ──http──▶ nginx :80 ──▶ 127.0.0.1:5080 (appen)
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

## 2. Cloudflare Tunnel og nginx

Som de andre apps på Pi'en (budget, madplan, wowanalyser …): tunnellen `homeassistent` sender alle
hostnavne til **nginx på port 80**, og nginx vælger appen ud fra `server_name`.

- **nginx:** `install-pi.sh` installerer [`nginx-familyexpenses.conf`](nginx-familyexpenses.conf) som
  `/etc/nginx/sites-enabled/familyexpenses` og kører `systemctl reload nginx` (ingen afbrydelse).
  Uden den blok viser adressen CampingLog, som ligger først i `sites-enabled`.
- **DNS:** en proxied CNAME `turkassen` → `e3513b09-569f-4514-9c9c-53b5c8537817.cfargotunnel.com`
  (vises som type *Tunnel*). Er oprettet.
- **Tunnel-regel:** tunnellen er **lokalt styret** – dashboardet tilbyder kun at migrere den, så tryk
  ikke på det. Reglerne ligger i `/home/ferreus/.cloudflared/config.yml` (ejet af `ferreus`, kun læsbar
  med sudo). Tag en backup, tilføj reglen over den sidste `http_status:404`, valider og genstart:

  ```yaml
    - hostname: turkassen.mathiasspangsberg.com
      service: http://localhost:80
  ```

  ```bash
  sudo cp /home/ferreus/.cloudflared/config.yml /home/ferreus/.cloudflared/config.yml.bak-$(date +%Y%m%d-%H%M%S)
  sudo cloudflared tunnel --config /home/ferreus/.cloudflared/config.yml ingress validate
  sudo systemctl restart cloudflared
  ```

  En genstart af cloudflared afbryder kortvarigt **alle** hostnavne på tunnellen, også Home Assistant.

Tjek bagefter: `https://turkassen.mathiasspangsberg.com/healthz` skal vise `Healthy`.

## 3. Første bruger

Åbn `https://turkassen.mathiasspangsberg.com/Account/Register` og log ind med Google. **Den første bruger bliver ejer**.
Derefter kan nye konti kun oprettes via invitationslinks fra appen.

## 4. Automatisk deploy

Når en pull request merges til `main`, kører CI. Er bygget grønt, installerer workflowet
[`deploy.yml`](../.github/workflows/deploy.yml) den samme `linux-arm64`-build på Pi'en gennem en
GitHub Actions-runner på Pi'en (labels `pi, familyexpenses`), ligesom wowanalyser. Runneren forbinder selv
ud til GitHub, så intet nyt eksponeres. Pull requests deployes aldrig, og `main` er beskyttet: ændringer
skal ind via en pull request med grøn CI.

Opsætning af runneren (én gang, og igen hvis den står som *Offline* på GitHub) – fra din egen maskine:

```bash
gh api -X POST repos/ferreus-debug/FamilyExpenses/actions/runners/registration-token --jq .token
```

```bash
scp deploy/setup-runner.sh pi@192.168.68.53:/tmp/ && ssh pi@192.168.68.53 "bash /tmp/setup-runner.sh <token>"
```

Runneren ligger i `~/actions-runner-familyexpenses` og kører som `pi`. Bruger appen en anden port end 5080,
så sæt repo-variablen `PI_PORT` (Settings → Secrets and variables → Actions → Variables).

Deploys kan følges under **Actions** → **Deploy**, og miljøet **production** viser, hvilken commit der kører.
Svarer appen ikke på `/healthz` efter installationen, fejler deployet – se `journalctl -u familyexpenses -n 50`
og ret fejlen med en ny pull request (eller revert den seneste).

> Runneren kører kun kode, der er merget til `main`, men som `pi` med sudo. Giv derfor kun skriveadgang til
> repoet til folk, du stoler på.

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

- **502 / "Bad gateway":** appen kører ikke. Tjek `curl http://127.0.0.1:5080/healthz` på Pi'en.
- **Adressen viser en anden app (fx CampingLog):** nginx-blokken mangler – kør deployet igen, eller tjek
  `ls /etc/nginx/sites-enabled`.
- **Login virker ikke / man bliver logget ud:** nginx skal sende cloudflareds `X-Forwarded-Proto: https` videre
  (det gør `nginx-familyexpenses.conf`), ikke `$scheme`.
- **Invitationslinks starter med `http://`:** samme årsag som ovenfor.
