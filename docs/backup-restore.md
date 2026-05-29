# Redhead Catalog — Backup & Restore Runbook

This document describes the current PostgreSQL backup setup for the Redhead Catalog production VPS.

## Current backup strategy

### PostgreSQL database backup

The production PostgreSQL database is backed up automatically from the VPS using a shell script.

Current setup:

- VPS app directory: `/opt/readhead-catalog/app`
- PostgreSQL Docker Compose service: `postgres`
- Database name: `redhead_sites_catalog`
- Database user: `postgres`
- Backup script: `/opt/readhead-catalog/app/scripts/backup/backup-postgres.sh`
- Backup config: `/etc/redhead/backup.env`
- Local backup directory: `/var/backups/redhead/postgres`
- Log file: `/var/log/redhead-postgres-backup.log`
- Remote storage: Google Workspace Shared Drive `Redhead Technical Backups`
- Remote folder: `PostgreSQL`
- rclone remote path: `redhead-tech-backups:PostgreSQL`

Schedule:

```cron
0 3 * * * /opt/readhead-catalog/app/scripts/backup/backup-postgres.sh
```

This runs daily at 03:00 server time.

Retention:

- Local VPS backups: 7 days
- Google Shared Drive backups: 30 days

The database backup is created with `pg_dump` in PostgreSQL custom format (`.dump`).

### Emergency Excel catalog export

The business emergency Excel export is separate from the PostgreSQL backup.

Target location:

- Google Workspace Shared Drive: `Redhead Emergency Catalog`

Purpose:

- Give employees access to the latest full sites catalog if the application is temporarily unavailable.
- This is a business snapshot, not a full system backup.

The Excel export should not contain technical/internal database data unless explicitly intended.

## Important access rules

### Redhead Technical Backups

Access should be limited to:

- App owner
- Developer
- backup service account

Employees should not have access to this Shared Drive because PostgreSQL dumps may contain users, roles, settings, internal notes, and other system data.

### Redhead Emergency Catalog

Access can be granted to employees.

Recommended employee role:

- Viewer

This allows employees to open/download the emergency Excel file without being able to modify or delete backup files.

## Secrets and config

Secrets must never be committed to Git.

These files exist only on the VPS:

```text
/etc/redhead/backup.env
/etc/redhead/secrets/google-service-account.json
/root/.config/rclone/rclone.conf
```

The service account JSON key is sensitive and must not be shared in Telegram, committed to the repository, uploaded to Cursor/Codex, or stored in public places.

Recommended file permissions:

```bash
chmod 600 /etc/redhead/backup.env
chmod 600 /etc/redhead/secrets/google-service-account.json
```

## How to manually run a backup

SSH into the VPS and run:

```bash
cd /opt/readhead-catalog/app
./scripts/backup/backup-postgres.sh
```

Check logs:

```bash
tail -100 /var/log/redhead-postgres-backup.log
```

Check local backup files:

```bash
ls -lh /var/backups/redhead/postgres
```

Check remote backup files in Google Shared Drive:

```bash
rclone ls redhead-tech-backups:PostgreSQL
```

A successful backup log should contain:

```text
Backup file created
Remote upload verified
Backup completed successfully
```

## How to check cron

List root crontab:

```bash
crontab -l
```

Expected line:

```cron
0 3 * * * /opt/readhead-catalog/app/scripts/backup/backup-postgres.sh
```

## How to check rclone access

List available Shared Drives visible to the service account:

```bash
rclone backend drives redhead-drive:
```

List PostgreSQL backups:

```bash
rclone ls redhead-tech-backups:PostgreSQL
```

Upload a test file if needed:

```bash
echo "Redhead backup test $(date -u)" > /tmp/redhead-backup-test.txt
rclone copy /tmp/redhead-backup-test.txt redhead-tech-backups:PostgreSQL
rclone ls redhead-tech-backups:PostgreSQL
```

Delete the test file afterwards:

```bash
rclone delete redhead-tech-backups:PostgreSQL/redhead-backup-test.txt
```

## Manual backup command without script

Use this only for debugging or emergency manual backup.

```bash
cd /opt/readhead-catalog/app
mkdir -p /var/backups/redhead/postgres

BACKUP_FILE="/var/backups/redhead/postgres/redhead-prod-postgres-$(date -u +'%Y-%m-%dT%H-%M-%SZ').dump"

docker compose exec -T postgres \
  pg_dump -U postgres -d redhead_sites_catalog -F c --no-owner --no-privileges \
  > "$BACKUP_FILE"

ls -lh "$BACKUP_FILE"
rclone copy "$BACKUP_FILE" redhead-tech-backups:PostgreSQL
rclone ls redhead-tech-backups:PostgreSQL
```

## Restore overview

Restoring production must be done carefully. A restore can overwrite the current production database.

Do not run destructive restore commands without:

1. confirming the target database;
2. stopping the application container;
3. making a fresh backup of the current database first;
4. having explicit approval from the business owner.

High-level restore flow:

```text
1. Download/select backup dump from Google Shared Drive.
2. Copy it to the VPS.
3. Stop the application container.
4. Recreate or clean the target database.
5. Restore using pg_restore.
6. Start the application container.
7. Verify health endpoint, login, Sites count, filters, and export.
```

A safe production restore script should be implemented separately with a strong confirmation prompt, for example requiring the operator to type:

```text
RESTORE_PRODUCTION_DATABASE
```


## Restore from Google Shared Drive into a test database

This is the safest way to verify that a backup stored in Google Shared Drive can actually be restored.

This flow does **not** touch the production database. It downloads the latest dump from Google Drive and restores it into a temporary database named `redhead_restore_test`.

### 1. Download the latest backup from Google Shared Drive

```bash
cd /opt/readhead-catalog/app

mkdir -p /tmp/redhead-restore-from-drive

LATEST_REMOTE_BACKUP="$(rclone lsf redhead-tech-backups:PostgreSQL --files-only | grep '^redhead-prod-postgres-.*\.dump$' | sort | tail -n 1)"

echo "$LATEST_REMOTE_BACKUP"

rclone copy "redhead-tech-backups:PostgreSQL/$LATEST_REMOTE_BACKUP" /tmp/redhead-restore-from-drive

ls -lh "/tmp/redhead-restore-from-drive/$LATEST_REMOTE_BACKUP"
```

### 2. Check that PostgreSQL can read the downloaded dump

```bash
docker compose exec -T postgres pg_restore --list < "/tmp/redhead-restore-from-drive/$LATEST_REMOTE_BACKUP" | head -40
```

Expected result: the command should print dump metadata and database objects such as tables, indexes, constraints, and extensions.

### 3. Restore the downloaded dump into a temporary test database

```bash
cd /opt/readhead-catalog/app

LATEST_REMOTE_BACKUP="$(rclone lsf redhead-tech-backups:PostgreSQL --files-only | grep '^redhead-prod-postgres-.*\.dump$' | sort | tail -n 1)"
DOWNLOADED_BACKUP="/tmp/redhead-restore-from-drive/$LATEST_REMOTE_BACKUP"
TEST_DB="redhead_restore_test"

docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS ${TEST_DB};"
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE ${TEST_DB};"

docker compose exec -T postgres pg_restore \
  -U postgres \
  -d "${TEST_DB}" \
  --no-owner \
  --no-privileges \
  --verbose \
  < "$DOWNLOADED_BACKUP"
```

### 4. Verify restored data

```bash
docker compose exec -T postgres psql -U postgres -d redhead_restore_test -c '
SELECT COUNT(*) AS sites_count FROM "Sites";
'

docker compose exec -T postgres psql -U postgres -d redhead_restore_test -c '
SELECT COUNT(*) AS users_count FROM "AspNetUsers";
'
```

Expected result: counts should be consistent with the production database at the moment the dump was created.

Example from the initial verification:

```text
sites_count = 67710
users_count = 14
```

### 5. Clean up the temporary test database and downloaded dump

```bash
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "
DROP DATABASE IF EXISTS redhead_restore_test;
"

rm -rf /tmp/redhead-restore-from-drive
```

Verify that the temporary database is gone:

```bash
docker compose exec -T postgres psql -U postgres -d postgres -c "\l" | grep redhead_restore_test
```

Expected result: no output.

## Safe restore test recommendation

At least once after setting up backups, test restore into a non-production database or local environment.

Recommended test:

```text
1. Download latest .dump file.
2. Restore it into a local/dev PostgreSQL instance.
3. Run the application against the restored database.
4. Check that users, Sites, filters, and exports work.
```

Backup without a tested restore is not a complete backup strategy.

## Disaster recovery notes

If the VPS is lost completely, the recovery inputs are:

- GitHub repository with application code and backup scripts;
- production environment values;
- Google service account key or replacement key;
- rclone config or ability to recreate it;
- latest PostgreSQL `.dump` from `Redhead Technical Backups`;
- Docker Compose deployment documentation;
- domain/DNS access;
- Caddy/reverse proxy configuration.

Current provider snapshots/backups can be useful for quickly rolling back the full VPS, but they should not be treated as the only database backup mechanism.
