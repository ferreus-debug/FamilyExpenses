#!/usr/bin/env bash
# Consistent online backup of the SQLite database (safe while the app is running),
# compressed and rotated. Installed as /usr/local/bin/familyexpenses-backup.
#   DB          database file          (default /var/lib/familyexpenses/familyexpenses.db)
#   BACKUP_DIR  where backups go        (default /var/lib/familyexpenses/backups; e.g. a USB disk)
#   KEEP_DAYS   delete older backups    (default 30)
set -euo pipefail

DB=${DB:-/var/lib/familyexpenses/familyexpenses.db}
BACKUP_DIR=${BACKUP_DIR:-/var/lib/familyexpenses/backups}
KEEP_DAYS=${KEEP_DAYS:-30}

if [[ ! -f "$DB" ]]; then
    echo "Database not found: $DB" >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"
target="$BACKUP_DIR/familyexpenses-$(date +%Y-%m-%d-%H%M%S).db"

# .backup uses SQLite's online backup API, so the copy is consistent even during writes.
sqlite3 "$DB" ".backup '$target'"
if [[ "$(sqlite3 "$target" 'PRAGMA integrity_check;')" != "ok" ]]; then
    echo "Integrity check failed for $target" >&2
    rm -f "$target"
    exit 1
fi
gzip -f "$target"

find "$BACKUP_DIR" -name 'familyexpenses-*.db.gz' -type f -mtime "+$KEEP_DAYS" -delete
echo "Backup written: $target.gz"
