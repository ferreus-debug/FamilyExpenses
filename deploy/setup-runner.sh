#!/usr/bin/env bash
# Registers a GitHub Actions self-hosted runner on the Pi for this repo (labels: pi,
# familyexpenses) and installs it as a service, like the other apps' runners. The Deploy
# workflow runs on it – GitHub's own runners can't reach the Pi on the home network.
#
# Runners on a personal account belong to one repo, so this lives in its own folder:
# ~/actions-runner-familyexpenses.
#
# Get a registration token (valid 1 hour) on your PC:
#   gh api -X POST repos/ferreus-debug/FamilyExpenses/actions/runners/registration-token --jq .token
# then on the Pi, as pi:
#   bash setup-runner.sh <token>
set -euo pipefail

TOKEN=${1:?"Brug: setup-runner.sh <registreringstoken>"}
REPO_URL=https://github.com/ferreus-debug/FamilyExpenses
NAME=pi-familyexpenses
LABELS=pi,familyexpenses
DIR=$HOME/actions-runner-familyexpenses

# Re-registering is the repair path when GitHub has dropped an offline runner's registration;
# the runner's auto-update can also leave .runner_migrated behind, which blocks config.sh.
if [[ -f "$DIR/.runner" || -f "$DIR/.runner_migrated" ]]; then
    echo "==> Registrerer den eksisterende runner i $DIR igen"
    cd "$DIR"
    sudo ./svc.sh stop >/dev/null 2>&1 || true
    sudo ./svc.sh uninstall >/dev/null 2>&1 || true
    sudo rm -f .runner .runner_migrated .credentials .credentials_rsaparams
else
    mkdir -p "$DIR"
    cd "$DIR"
    version=$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest \
        | sed -n 's/.*"tag_name": *"v\([^"]*\)".*/\1/p')
    if [[ -z "$version" ]]; then
        echo "Kunne ikke finde seneste runner-version." >&2
        exit 1
    fi
    echo "==> Henter GitHub Actions runner $version (linux-arm64)"
    curl -fsSL "https://github.com/actions/runner/releases/download/v$version/actions-runner-linux-arm64-$version.tar.gz" \
        | tar -xz
fi

echo "==> Registrerer runneren"
./config.sh --unattended --url "$REPO_URL" --token "$TOKEN" \
    --name "$NAME" --labels "$LABELS" --work _work --replace

echo "==> Starter runneren som systemd-tjeneste"
sudo ./svc.sh install "$USER"
sudo ./svc.sh start
echo "Runneren kører og står under GitHub → Settings → Actions → Runners."
