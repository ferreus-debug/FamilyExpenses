#!/usr/bin/env bash
# Rolls Fællesudgifter back to an earlier release that is still on the Pi. Installed by install-pi.sh
# as /opt/familyexpenses/bin/rollback.sh:
#   sudo /opt/familyexpenses/bin/rollback.sh              # to the release before the live one
#   sudo /opt/familyexpenses/bin/rollback.sh r12-b9886e9  # to a specific release
#   sudo /opt/familyexpenses/bin/rollback.sh --list       # show what's available
# Also run by the Rollback workflow (.github/workflows/rollback.yml), so it can be done from GitHub.
# Only the code goes back – the database stays as it is (backups: /var/lib/familyexpenses/backups).
set -euo pipefail

APP_DIR=/opt/familyexpenses
RELEASES=$APP_DIR/releases
ENV_FILE=/etc/familyexpenses/familyexpenses.env

LIVE=$(basename "$(readlink -f "$APP_DIR/current" 2>/dev/null)" 2>/dev/null || true)
# Oldest first by folder time, i.e. in deploy order.
mapfile -t ALL < <(find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %f\n' 2>/dev/null | sort -n | cut -d' ' -f2-)

if [[ "${1:-}" == "--list" ]]; then
    for r in "${ALL[@]}"; do
        if [[ "$r" == "$LIVE" ]]; then echo "* $r (live)"; else echo "  $r"; fi
    done
    exit 0
fi

if [[ $EUID -ne 0 ]]; then
    echo "Kør scriptet med sudo." >&2
    exit 1
fi

TARGET=${1:-}
if [[ -z "$TARGET" ]]; then
    # The newest release older than the live one.
    for r in "${ALL[@]}"; do
        [[ "$r" == "$LIVE" ]] && break
        TARGET=$r
    done
fi

if [[ -z "$TARGET" || ! $TARGET =~ ^[A-Za-z0-9._-]+$ || ! -d "$RELEASES/$TARGET" ]]; then
    echo "Ingen release at rulle tilbage til${TARGET:+ ($TARGET findes ikke)}. Tilgængelige:" >&2
    for r in "${ALL[@]}"; do echo "  $r" >&2; done
    exit 1
fi
if [[ "$TARGET" == "$LIVE" ]]; then
    echo "$TARGET er allerede live."
    exit 0
fi

echo "==> Ruller tilbage: $LIVE -> $TARGET"
ln -sfn "$RELEASES/$TARGET" "$APP_DIR/current.tmp"
mv -Tf "$APP_DIR/current.tmp" "$APP_DIR/current"
systemctl restart familyexpenses

url=$(grep -E '^ASPNETCORE_URLS=' "$ENV_FILE" | cut -d= -f2)
for _ in $(seq 1 30); do
    if curl -fsS "$url/healthz" >/dev/null 2>&1; then
        # Releases from before /version existed (r0-initial) only have /healthz.
        version=$(curl -fsS "$url/version" 2>/dev/null || true)
        if [[ -z "$version" || "$version" == "$TARGET" ]]; then
            echo "OK: $TARGET er live"
            exit 0
        fi
    fi
    sleep 1
done
echo "FEJL: $TARGET svarer ikke sundt. Se: journalctl -u familyexpenses -n 50" >&2
exit 1
