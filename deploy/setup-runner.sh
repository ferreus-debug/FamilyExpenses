#!/usr/bin/env bash
# One-time setup of automatic deployment on the Raspberry Pi: installs a GitHub Actions
# self-hosted runner for this repository and lets it run the deploy script with sudo.
# Run on the Pi from a checkout or copy of the repository:
#   sudo deploy/setup-runner.sh <registreringstoken>
# Get the token on GitHub: Settings → Actions → Runners → New self-hosted runner (valid for 1 hour).
set -euo pipefail

TOKEN=${1:?"Brug: sudo deploy/setup-runner.sh <registreringstoken>"}
REPO_URL=https://github.com/ferreus-debug/FamilyExpenses
RUNNER_USER=github-runner
RUNNER_DIR=/opt/actions-runner
HERE=$(cd "$(dirname "$0")" && pwd)

if [[ $EUID -ne 0 ]]; then
    echo "Kør scriptet med sudo." >&2
    exit 1
fi

if [[ "$(uname -m)" != "aarch64" ]]; then
    echo "Pi'en skal køre et 64-bit OS (uname -m = aarch64)." >&2
    exit 1
fi

echo "==> Systembruger '$RUNNER_USER'"
id "$RUNNER_USER" >/dev/null 2>&1 \
    || useradd --system --create-home --home-dir "/home/$RUNNER_USER" --shell /usr/sbin/nologin "$RUNNER_USER"

echo "==> Deploy-script og sudo-regel"
install -m 0755 -o root -g root "$HERE/familyexpenses-deploy.sh" /usr/local/sbin/familyexpenses-deploy
sudoers=$(mktemp)
echo "$RUNNER_USER ALL=(root) NOPASSWD: /usr/local/sbin/familyexpenses-deploy" > "$sudoers"
visudo -cf "$sudoers" >/dev/null
install -m 0440 -o root -g root "$sudoers" /etc/sudoers.d/familyexpenses-deploy
rm -f "$sudoers"

if [[ -f "$RUNNER_DIR/.runner" ]]; then
    echo "Runneren er allerede registreret i $RUNNER_DIR. Fjern den først for at registrere igen:" >&2
    echo "  cd $RUNNER_DIR && sudo ./svc.sh uninstall && sudo -u $RUNNER_USER ./config.sh remove --token <token>" >&2
    exit 1
fi

echo "==> Henter GitHub Actions runner"
version=$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest \
    | sed -n 's/.*"tag_name": *"v\([^"]*\)".*/\1/p')
if [[ -z "$version" ]]; then
    echo "Kunne ikke finde seneste runner-version." >&2
    exit 1
fi
install -d -m 0755 -o "$RUNNER_USER" -g "$RUNNER_USER" "$RUNNER_DIR"
curl -fsSL "https://github.com/actions/runner/releases/download/v$version/actions-runner-linux-arm64-$version.tar.gz" \
    | sudo -u "$RUNNER_USER" tar -xz -C "$RUNNER_DIR"
"$RUNNER_DIR/bin/installdependencies.sh"

echo "==> Registrerer runneren"
sudo -u "$RUNNER_USER" "$RUNNER_DIR/config.sh" --unattended \
    --url "$REPO_URL" --token "$TOKEN" \
    --name "$(hostname)-familyexpenses" --labels familyexpenses --work _work --replace

echo "==> Starter runneren som systemd-tjeneste"
cd "$RUNNER_DIR"
./svc.sh install "$RUNNER_USER"
./svc.sh start

echo
echo "Færdig. Runneren skal nu stå som 'Idle' under Settings → Actions → Runners på GitHub."
