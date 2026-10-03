#!/usr/bin/env bash
# Builds for the Pi (linux-arm64, self-contained) and installs/updates it over SSH.
# Run from your own machine (Linux, macOS or WSL):
#   deploy/publish-pi.sh pi@raspberrypi.local [port]
# Requires: .NET 10 SDK, rsync and ssh access to the Pi with sudo rights.
set -euo pipefail

TARGET=${1:?"Brug: deploy/publish-pi.sh bruger@pi-adresse [port]"}
PORT=${2:-5080}
ROOT=$(cd "$(dirname "$0")/.." && pwd)
OUT="$ROOT/artifacts/pi"
REMOTE_DIR=familyexpenses-release

echo "==> Bygger til linux-arm64"
rm -rf "$OUT"
dotnet publish "$ROOT/src/FamilyExpenses.Web/FamilyExpenses.Web.csproj" \
    --configuration Release --runtime linux-arm64 --self-contained -o "$OUT" -nologo

echo "==> Kopierer til $TARGET:~/$REMOTE_DIR"
# shellcheck disable=SC2029 # REMOTE_DIR is meant to expand locally
ssh "$TARGET" "mkdir -p ~/$REMOTE_DIR/app ~/$REMOTE_DIR/deploy"
rsync -az --delete "$OUT/" "$TARGET:$REMOTE_DIR/app/"
rsync -az --delete --exclude publish-pi.sh "$ROOT/deploy/" "$TARGET:$REMOTE_DIR/deploy/"

echo "==> Installerer på Pi'en (beder om sudo-kode)"
# shellcheck disable=SC2029
ssh -t "$TARGET" "sudo ~/$REMOTE_DIR/deploy/install-pi.sh $PORT"
