#!/usr/bin/env bash
# Entry point the GitHub Actions runner on the Pi may run with sudo (installed by setup-runner.sh
# as /usr/local/sbin/familyexpenses-deploy). Copies a release folder (app/ + deploy/) to a
# root-owned staging folder and installs it from there:
#   sudo familyexpenses-deploy /path/to/release [port]
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
