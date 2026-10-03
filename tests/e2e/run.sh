#!/usr/bin/env bash
# Starts the web app on a fresh SQLite file, runs flow.mjs against it and stops the app.
# Usage: tests/e2e/run.sh   (needs .NET 10, Node 20+ and a Chromium; set CHROME_PATH if
# Playwright's own browser isn't installed, e.g. CHROME_PATH=/usr/bin/chromium)
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
PORT=${PORT:-5058}
DB=$(mktemp -d)/e2e.db

[ -d "$HERE/node_modules" ] || (cd "$HERE" && npm install --silent)
mkdir -p "$HERE/shots"

dotnet build "$ROOT/src/FamilyExpenses.Web" -v quiet -nologo
ConnectionStrings__Default="Data Source=$DB" ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-build --project "$ROOT/src/FamilyExpenses.Web" --urls "http://127.0.0.1:$PORT" > "$HERE/shots/app.log" 2>&1 &
APP=$!
trap 'pkill -P $APP 2>/dev/null; kill $APP 2>/dev/null; rm -f "$DB"*' EXIT

for _ in $(seq 1 60); do curl -s -o /dev/null "http://127.0.0.1:$PORT/" && break; sleep 1; done
cd "$HERE" && BASE="http://127.0.0.1:$PORT" node flow.mjs
