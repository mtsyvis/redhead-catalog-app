#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${REDHEAD_BACKUP_ENV_FILE:-/etc/redhead/backup.env}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Backup config file not found: $ENV_FILE" >&2
  exit 1
fi

# shellcheck source=/dev/null
source "$ENV_FILE"

required_vars=(
  APP_DIR
  BACKUP_DIR
  LOG_FILE
  COMPOSE_SERVICE
  DB_USER
  DB_NAME
  RCLONE_REMOTE_PATH
  LOCAL_RETENTION_DAYS
  REMOTE_RETENTION_DAYS
  FILE_PREFIX
)

for var_name in "${required_vars[@]}"; do
  if [[ -z "${!var_name:-}" ]]; then
    echo "Required variable is missing in $ENV_FILE: $var_name" >&2
    exit 1
  fi
done

mkdir -p "$BACKUP_DIR"
mkdir -p "$(dirname "$LOG_FILE")"

exec >> "$LOG_FILE" 2>&1

LOCK_FILE="${LOCK_FILE:-/var/lock/redhead-postgres-backup.lock}"
exec 9>"$LOCK_FILE"

if ! flock -n 9; then
  echo "[$(date -u +'%Y-%m-%dT%H:%M:%SZ')] Another backup process is already running. Exiting."
  exit 1
fi

timestamp="$(date -u +'%Y-%m-%dT%H-%M-%SZ')"
file_name="${FILE_PREFIX}-${timestamp}.dump"
local_file="${BACKUP_DIR}/${file_name}"
tmp_file="${local_file}.tmp"

cleanup() {
  rm -f "$tmp_file"
}

trap cleanup EXIT

echo "[$(date -u +'%Y-%m-%dT%H:%M:%SZ')] Starting PostgreSQL backup..."
echo "App dir: $APP_DIR"
echo "Database: $DB_NAME"
echo "Local file: $local_file"
echo "Remote path: $RCLONE_REMOTE_PATH"

cd "$APP_DIR"

docker compose exec -T "$COMPOSE_SERVICE" \
  pg_dump -U "$DB_USER" -d "$DB_NAME" -F c --no-owner --no-privileges \
  > "$tmp_file"

if [[ ! -s "$tmp_file" ]]; then
  echo "Backup failed: dump file is empty."
  exit 1
fi

mv "$tmp_file" "$local_file"

echo "Backup file created:"
ls -lh "$local_file"

echo "Uploading backup to Google Drive..."
rclone copy "$local_file" "$RCLONE_REMOTE_PATH"

echo "Checking remote file..."
if ! rclone lsf "$RCLONE_REMOTE_PATH" --files-only | grep -Fxq "$file_name"; then
  echo "Backup upload verification failed: remote file not found."
  exit 1
fi

echo "Remote upload verified: ${RCLONE_REMOTE_PATH}/${file_name}"

echo "Cleaning local backups older than ${LOCAL_RETENTION_DAYS} days..."
find "$BACKUP_DIR" \
  -type f \
  -name "${FILE_PREFIX}-*.dump" \
  -mtime "+${LOCAL_RETENTION_DAYS}" \
  -delete

echo "Cleaning remote backups older than ${REMOTE_RETENTION_DAYS} days..."
rclone delete "$RCLONE_REMOTE_PATH" \
  --include "${FILE_PREFIX}-*.dump" \
  --min-age "${REMOTE_RETENTION_DAYS}d"

echo "[$(date -u +'%Y-%m-%dT%H:%M:%SZ')] Backup completed successfully."
echo
