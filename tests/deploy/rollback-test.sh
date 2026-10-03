#!/usr/bin/env bash
# Tests the Pi's deploy and rollback scripts (deploy/install-pi.sh, deploy/rollback.sh) without a Pi:
# the real scripts run with their paths redirected into a temp folder and the system commands
# (systemctl, curl, nginx, ...) replaced by stubs. A fake release counts as healthy if it has a
# "healthy" marker file, and reports the contents of its VERSION file on /version.
# Runs in CI; locally (Linux/WSL):  tests/deploy/rollback-test.sh
set -euo pipefail
REPO=$(cd "$(dirname "$0")/../.." && pwd)
S=$(mktemp -d)
trap 'rm -rf "$S"' EXIT
mkdir -p "$S/bin" "$S/opt" "$S/etc" "$S/systemd" "$S/nginx/sites-available" "$S/nginx/sites-enabled" "$S/var" "$S/src"

for c in systemctl journalctl nginx useradd apt-get chown id ss sleep sqlite3; do printf '#!/bin/sh\nexit 0\n' > "$S/bin/$c"; done
printf '#!/bin/sh\necho aarch64\n' > "$S/bin/uname"
cat > "$S/bin/curl" <<EOF
#!/bin/bash
url="\${@: -1}"; cur=$S/opt/current
case "\$url" in
  */healthz) [ -f "\$cur/healthy" ] || exit 22; echo Healthy ;;
  */version) [ -f "\$cur/VERSION" ] || exit 22; cat "\$cur/VERSION" ;;
  *) exit 22 ;;
esac
EOF
chmod +x "$S"/bin/*
export PATH="$S/bin:$PATH"

# The last expression is a literal $EUID in the scripts, so they run without root here.
# shellcheck disable=SC2016
redirect() {
    tr -d '\r' < "$1" | sed \
        -e "s|/opt/familyexpenses|$S/opt|g" \
        -e "s|/etc/familyexpenses|$S/etc|g" \
        -e "s|/etc/systemd/system|$S/systemd|g" \
        -e "s|/etc/nginx|$S/nginx|g" \
        -e "s|/usr/local/bin/familyexpenses-backup|$S/backup|g" \
        -e "s|/var/lib/familyexpenses|$S/var|g" \
        -e 's/\$EUID -ne 0/0 -ne 0/'
}
mkdir -p "$S/deploy"
for f in "$REPO"/deploy/*; do redirect "$f" > "$S/deploy/$(basename "$f")"; done
chmod +x "$S"/deploy/*.sh

# release <name> <healthy:yes|no> [reported-version]
release() {
    local dir=$S/src/$1
    mkdir -p "$dir/app"
    cp -r "$S/deploy" "$dir/deploy"
    printf '#!/bin/sh\n' > "$dir/app/FamilyExpenses.Web"; chmod +x "$dir/app/FamilyExpenses.Web"
    [ "$2" = yes ] && touch "$dir/app/healthy"
    echo -n "${3:-$1}" > "$dir/app/VERSION"
    echo "$dir"
}
install_release() { "$(release "$@")/deploy/install-pi.sh" 5080 "$1" > "$S/log" 2>&1; }
live() { basename "$(readlink -f "$S/opt/current")"; }
# Entries in a folder, sorted, on one line.
names() { find "$1" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort | tr '\n' ' '; }
pass=0; fail=0
check() { if [ "$2" = "$3" ]; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1: expected '$3', got '$2'"; fail=$((fail+1)); cat "$S/log"; fi; }

echo "1. migrate the old flat install, then deploy r1"
printf '#!/bin/sh\n' > "$S/opt/FamilyExpenses.Web"; chmod +x "$S/opt/FamilyExpenses.Web"; touch "$S/opt/healthy" "$S/opt/appsettings.json"
mkdir -p "$S/var"; touch "$S/var/familyexpenses.db"
install_release r1-aaaaaaa yes && rc=0 || rc=$?
check "exit code" "$rc" 0
check "live" "$(live)" r1-aaaaaaa
check "old install kept as r0-initial" "$(names "$S/opt/releases/r0-initial")" "FamilyExpenses.Web appsettings.json healthy "
check "flat files gone from app dir" "$(names "$S/opt")" "bin current releases "
check "rollback script installed" "$(test -x "$S/opt/bin/rollback.sh" && echo yes)" yes
check "service runs from current" "$(grep -c '^ExecStart=.*/current/FamilyExpenses.Web' "$S/systemd/familyexpenses.service")" 1

echo "2. broken release rolls back by itself"
install_release r2-bbbbbbb no && rc=0 || rc=$?
check "exit code" "$rc" 1
check "live" "$(live)" r1-aaaaaaa
check "log says rolled back" "$(grep -c 'Ruller tilbage til r1-aaaaaaa' "$S/log")" 1

echo "3. release that answers with the wrong version rolls back"
install_release r3-ccccccc yes r1-aaaaaaa && rc=0 || rc=$?
check "exit code" "$rc" 1
check "live" "$(live)" r1-aaaaaaa

echo "4. many good deploys keep 5 releases, including the live one"
for n in 4 5 6 7 8 9; do install_release "r$n-ddddddd" yes; done
check "live" "$(live)" r9-ddddddd
check "kept" "$(find "$S/opt/releases" -mindepth 1 -maxdepth 1 | wc -l)" 5
check "newest kept" "$(names "$S/opt/releases")" "r5-ddddddd r6-ddddddd r7-ddddddd r8-ddddddd r9-ddddddd "

echo "5. manual rollback"
"$S/opt/bin/rollback.sh" > "$S/log" 2>&1 && rc=0 || rc=$?
check "no argument: exit" "$rc" 0
check "no argument: previous release" "$(live)" r8-ddddddd
"$S/opt/bin/rollback.sh" r6-ddddddd > "$S/log" 2>&1
check "named release" "$(live)" r6-ddddddd
check "--list marks live" "$("$S/opt/bin/rollback.sh" --list | grep '^\*')" "* r6-ddddddd (live)"
"$S/opt/bin/rollback.sh" ../../etc > "$S/log" 2>&1 && rc=0 || rc=$?
check "path traversal refused" "$rc" 1
check "live unchanged" "$(live)" r6-ddddddd

echo "6. redeploying after a rollback goes forward again"
install_release r10-eeeeeee yes
check "live" "$(live)" r10-eeeeeee

echo
echo "passed: $pass, failed: $fail"
[ "$fail" -eq 0 ]
