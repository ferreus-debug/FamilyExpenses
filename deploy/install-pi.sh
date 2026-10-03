#!/usr/bin/env bash
# Installs or updates Fællesudgifter on the Raspberry Pi. Idempotent: run it for every new version.
# Run on the Pi as root from the uploaded release folder (publish-pi.sh does this for you):
#   sudo ./deploy/install-pi.sh [port]
# Expects the published app in ../app relative to this script.
set -euo pipefail

PORT=${1:-5080}
HERE=$(cd "$(dirname "$0")" && pwd)
APP_SOURCE=$(cd "$HERE/../app" && pwd)
APP_DIR=/opt/familyexpenses
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

echo "==> App til $APP_DIR"
if systemctl is-active --quiet familyexpenses; then
    systemctl stop familyexpenses
fi
install -d -m 0755 "$APP_DIR"
rsync -a --delete "$APP_SOURCE/" "$APP_DIR/"
chown -R root:root "$APP_DIR"
chmod 0755 "$APP_DIR/FamilyExpenses.Web"

echo "==> systemd-enheder og backup-script"
install -m 0644 "$HERE/familyexpenses.service" /etc/systemd/system/familyexpenses.service
install -m 0644 "$HERE/familyexpenses-backup.service" /etc/systemd/system/familyexpenses-backup.service
install -m 0644 "$HERE/familyexpenses-backup.timer" /etc/systemd/system/familyexpenses-backup.timer
install -m 0755 "$HERE/backup.sh" /usr/local/bin/familyexpenses-backup
systemctl daemon-reload
systemctl enable --now familyexpenses-backup.timer
systemctl enable familyexpenses
systemctl start familyexpenses

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

echo "==> Tjekker at appen svarer"
for _ in $(seq 1 30); do
    if curl -fsS "$url/healthz" >/dev/null 2>&1; then
        echo "OK: $url/healthz svarer Healthy"
        echo
        echo "Næste skridt: tilføj en regel i Cloudflare Tunnel, der peger på $url (se deploy/README.md)."
        exit 0
    fi
    sleep 1
done

echo "Appen svarer ikke. Se loggen med: journalctl -u familyexpenses -n 50" >&2
exit 1
