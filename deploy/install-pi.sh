#!/usr/bin/env bash
# Installs or updates Fællesudgifter on the Raspberry Pi. Idempotent: run it for every new version.
# Run on the Pi as root from the uploaded release folder (the Deploy workflow and publish-pi.sh do this):
#   sudo ./deploy/install-pi.sh [port] [release-name]
# Expects the published app in ../app relative to this script.
#
# Every release gets its own folder under /opt/familyexpenses/releases/<release-name>, and
# /opt/familyexpenses/current is a symlink to the live one. Going live = swap the symlink + restart.
# If the new release doesn't answer /healthz and /version with its own name, the script switches
# back to the previous release by itself and fails, so a broken build never stays live.
# Manual rollback: /opt/familyexpenses/bin/rollback.sh (see rollback.sh).
set -euo pipefail

PORT=${1:-5080}
RELEASE=${2:-local-$(date +%Y%m%d-%H%M%S)}
KEEP=${KEEP:-5}   # releases kept on disk for rollback
HERE=$(cd "$(dirname "$0")" && pwd)
APP_SOURCE=$(cd "$HERE/../app" && pwd)
APP_DIR=/opt/familyexpenses
RELEASES=$APP_DIR/releases
CONFIG_DIR=/etc/familyexpenses
ENV_FILE=$CONFIG_DIR/familyexpenses.env

if [[ $EUID -ne 0 ]]; then
    echo "Kør scriptet med sudo." >&2
    exit 1
fi

if [[ "$(uname -m)" != "aarch64" ]]; then
    echo "Pi'en skal køre et 64-bit OS (uname -m = aarch64), men den siger '$(uname -m)'." >&2
    echo ".NET 10 understøtter ikke 32-bit ARM. Geninstallér med Raspberry Pi OS (64-bit)." >&2
    exit 1
fi

if [[ ! $RELEASE =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "Ugyldigt release-navn: '$RELEASE'" >&2
    exit 1
fi

if [[ ! -x "$APP_SOURCE/FamilyExpenses.Web" ]]; then
    echo "Fandt ikke den publicerede app i $APP_SOURCE." >&2
    exit 1
fi

# The port must not collide with the other apps on the Pi (budget, madplan, ...).
if ss -ltnH "sport = :$PORT" | grep -q . && ! systemctl is-active --quiet familyexpenses; then
    echo "Port $PORT er allerede i brug af en anden app:" >&2
    ss -ltnp "sport = :$PORT" >&2
    echo "Vælg en anden port, fx: sudo $0 5081" >&2
    exit 1
fi

echo "==> Pakker (sqlite3 til backup)"
command -v sqlite3 >/dev/null || apt-get install -y sqlite3

echo "==> Systembruger 'familyexpenses'"
id familyexpenses >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin familyexpenses

echo "==> Konfiguration ($ENV_FILE)"
install -d -m 0755 "$CONFIG_DIR"
if [[ ! -f "$ENV_FILE" ]]; then
    cat > "$ENV_FILE" <<ENV
# Lokale indstillinger for Fællesudgifter. Genstart efter ændringer: sudo systemctl restart familyexpenses
ASPNETCORE_URLS=http://127.0.0.1:$PORT
# Hvis cloudflared kører i Docker, så stol på Docker-netværket:
#ReverseProxy__KnownNetworks__0=172.17.0.0/16
# Backup til fx en USB-disk (skal være skrivbar for brugeren familyexpenses):
#BACKUP_DIR=/mnt/usb/familyexpenses-backup
#KEEP_DAYS=30
ENV
fi

echo "==> Release $RELEASE"
install -d -m 0755 "$APP_DIR" "$RELEASES" "$APP_DIR/bin"
# Before releases existed the app was installed straight into $APP_DIR. Keep that install as a
# release, so there is something to roll back to.
if [[ -f "$APP_DIR/FamilyExpenses.Web" && ! -L "$APP_DIR/current" ]]; then
    install -d -m 0755 "$RELEASES/r0-initial"
    find "$APP_DIR" -mindepth 1 -maxdepth 1 ! -name releases ! -name bin -exec mv -t "$RELEASES/r0-initial" {} +
    ln -sfn "$RELEASES/r0-initial" "$APP_DIR/current"
fi
DEST=$RELEASES/$RELEASE
rm -rf "$DEST"   # re-deploying the same release replaces it
install -d -m 0755 "$DEST"
cp -a "$APP_SOURCE/." "$DEST/"
chown -R root:root "$DEST"
chmod 0755 "$DEST/FamilyExpenses.Web"
install -m 0755 "$HERE/rollback.sh" "$APP_DIR/bin/rollback.sh"

echo "==> systemd-enheder og backup-script"
install -m 0644 "$HERE/familyexpenses.service" /etc/systemd/system/familyexpenses.service
install -m 0644 "$HERE/familyexpenses-backup.service" /etc/systemd/system/familyexpenses-backup.service
install -m 0644 "$HERE/familyexpenses-backup.timer" /etc/systemd/system/familyexpenses-backup.timer
install -m 0755 "$HERE/backup.sh" /usr/local/bin/familyexpenses-backup
systemctl daemon-reload
systemctl enable --now familyexpenses-backup.timer
systemctl enable familyexpenses

url=$(grep -E '^ASPNETCORE_URLS=' "$ENV_FILE" | cut -d= -f2)

# Same pattern as the other apps: the tunnel sends every hostname to nginx on port 80.
# reload (not restart) keeps the other sites up.
if command -v nginx >/dev/null; then
    echo "==> nginx-site"
    sed "s|http://127.0.0.1:5080|$url|" "$HERE/nginx-familyexpenses.conf" > /etc/nginx/sites-available/familyexpenses
    ln -sf /etc/nginx/sites-available/familyexpenses /etc/nginx/sites-enabled/familyexpenses
    nginx -t
    systemctl reload nginx
fi

# Healthy, and (when the build has /version) answering with the expected release name.
wait_healthy() {
    local want=$1 version
    for _ in $(seq 1 30); do
        if curl -fsS "$url/healthz" >/dev/null 2>&1; then
            version=$(curl -fsS "$url/version" 2>/dev/null || true)
            if [[ -z "$version" || "$version" == "$want" ]]; then
                return 0
            fi
        fi
        sleep 1
    done
    return 1
}

# Atomic symlink swap: a crash halfway never leaves "current" missing.
switch_to() {
    ln -sfn "$1" "$APP_DIR/current.tmp"
    mv -Tf "$APP_DIR/current.tmp" "$APP_DIR/current"
}

PREVIOUS=$(readlink -f "$APP_DIR/current" 2>/dev/null || true)
[[ "$PREVIOUS" == "$DEST" ]] && PREVIOUS=""

# A rollback restores the code, not the database: take a backup before a new release can migrate it.
if [[ -n "$PREVIOUS" && -f /var/lib/familyexpenses/familyexpenses.db ]]; then
    echo "==> Backup af databasen før opdatering"
    systemctl start familyexpenses-backup || echo "ADVARSEL: backup fejlede – fortsætter" >&2
fi

echo "==> Går live med $RELEASE"
switch_to "$DEST"
systemctl restart familyexpenses

if ! wait_healthy "$RELEASE"; then
    echo "FEJL: $RELEASE svarer ikke sundt. Seneste log:" >&2
    journalctl -u familyexpenses -n 40 --no-pager >&2 || true
    if [[ -n "$PREVIOUS" && -d "$PREVIOUS" ]]; then
        echo "==> Ruller tilbage til $(basename "$PREVIOUS")" >&2
        switch_to "$PREVIOUS"
        systemctl restart familyexpenses
        wait_healthy "$(basename "$PREVIOUS")" || echo "ADVARSEL: den tidligere version svarer heller ikke" >&2
    fi
    exit 1
fi
echo "OK: $RELEASE er live på $url"

echo "==> Rydder op (beholder $KEEP releases)"
live=$(basename "$(readlink -f "$APP_DIR/current")")
# Newest first by folder time (also orders manual local-* releases correctly); never the live one.
find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %f\n' | sort -rn | cut -d' ' -f2- \
    | tail -n +$((KEEP + 1)) | while read -r old; do
    [[ "$old" == "$live" ]] && continue
    rm -rf "${RELEASES:?}/$old"
    echo "   fjernede $old"
done
