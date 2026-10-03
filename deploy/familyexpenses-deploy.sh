#!/usr/bin/env bash
# Run by the Deploy workflow on the Pi's runner. Copies a release folder (app/ + deploy/) out of
# the runner's work folder to a root-owned staging folder and installs it from there:
#   sudo deploy/familyexpenses-deploy.sh /path/to/release [port]
set -euo pipefail

SOURCE=${1:?"Brug: familyexpenses-deploy /sti/til/release [port]"}
PORT=${2:-5080}
STAGING_ROOT=/var/lib/familyexpenses-deploy
STAGING=$STAGING_ROOT/release

if [[ $EUID -ne 0 ]]; then
    echo "Kør scriptet med sudo." >&2
    exit 1
fi

if [[ ! $PORT =~ ^[0-9]+$ ]]; then
    echo "Ugyldig port: '$PORT'" >&2
    exit 1
fi

if [[ ! -d "$SOURCE/app" || ! -f "$SOURCE/deploy/install-pi.sh" ]]; then
    echo "$SOURCE skal indeholde app/ og deploy/install-pi.sh." >&2
    exit 1
fi

echo "==> Kopierer release til $STAGING"
install -d -m 0700 "$STAGING_ROOT"
rm -rf "$STAGING"
cp -r "$SOURCE" "$STAGING"
chown -R root:root "$STAGING"
chmod 0755 "$STAGING/app/FamilyExpenses.Web" "$STAGING"/deploy/*.sh

exec "$STAGING/deploy/install-pi.sh" "$PORT"
