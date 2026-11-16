# Nine - Database Management Guide

**Version:** 1.2.0  
**Last Updated:** November 16, 2026  
**Audience:** Administrators and Power Users

---

## Table of Contents

1. [Overview](#overview)
2. [Database Location & Structure](#database-location--structure)
3. [Backup Procedures](#backup-procedures)
4. [Restore Procedures](#restore-procedures)
5. [Database Reset](#database-reset)
6. [Database Health Checks](#database-health-checks)
7. [Troubleshooting](#troubleshooting)
8. [Advanced Topics](#advanced-topics)
   - [Schema Migrations & Model Optimization](#schema-migrations--model-optimization)
9. [Best Practices](#best-practices)
10. [FAQ](#faq)

---

## Overview

Nine uses **SQLite** as its database engine - a lightweight, file-based database that requires no server installation or configuration. This guide covers everything you need to know about managing your database, protecting your data, and recovering from issues.

### What You'll Learn

- ✅ How to back up your database
- ✅ How to restore from a backup
- ✅ How to check database health
- ✅ How to troubleshoot common database issues
- ✅ Best practices for data protection

### Why Database Management Matters

Your database contains **all your property management data:**

- Properties and tenants
- Leases and financial records
- Invoices and payments
- Repairs and inspections
- Documents and photos
- User accounts and settings

**Regular backups** are your insurance policy against:

- Hardware failure (hard drive crash, SSD failure)
- Software bugs or corruption
- Accidental data deletion
- Ransomware or malware
- Natural disasters or theft

---

## Database Location & Structure

### Database File Location

**Linux:**

```bash
~/.config/Nine/app_v1.2.0.db
```

**Windows:**

```
%APPDATA%\Nine\app_v1.2.0.db
```

**Full path examples:**

- **Linux:** `/home/username/.config/Nine/app_v1.2.0.db`
- **Windows:** `C:\Users\YourName\AppData\Roaming\Nine\app_v1.2.0.db`

### Database Structure

The database file is a **single SQLite file** containing:

- All tables and indexes
- All data (properties, tenants, leases, etc.)
- Schema version information
- Configuration settings

**File size:** Varies based on usage

- New install: ~5 MB (with schema only)
- After 1 year: 10-50 MB (typical landlord)
- After 5 years: 50-200 MB (depends on photos/documents)

### Schema Version

Nine v1.2.0 uses schema version `1.2.0`. This tracks the database structure and ensures compatibility.

**Check your schema version:**

1. Navigate to **Application**
2. View **"Database Information"** panel
3. See **"Schema Version: 1.2.0"**

### Auto-Migration/Upgrade

When you upgrade Nine to a new version:

1. Application detects current schema version
2. Compares with required schema version
3. Creates a backup of the existing database if needed
4. Automatically applies migrations/upgrade if needed
5. Updates schema version in database
6. Application starts normally
7. All data is preserved

**No manual intervention required** - migrations are automatic and seamless!

**Version Upgrade Compatibility:**  
Not all version upgrades are supported automatically. Check the **[Compatibility Matrix](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Compatibility-Matrix.md)** before upgrading to see if your current version can auto-migrate to the target version, or if manual steps are required.

---

## Backup Procedures

### Why Back Up?

**Your database should be backed up regularly because:**

- Hardware can fail without warning
- Human error happens (accidental deletions)
- Software bugs can cause corruption
- You may need to revert to a previous state

**How often to back up:**

- **Before major changes** - Always (e.g., bulk imports, data migrations)
- **Daily** - Recommended for active property managers
- **Weekly** - Minimum for most users
- **Monthly** - Acceptable for very light usage

### Method 1: Manual Backup (Recommended Before Major Changes)

**Step-by-step:**

1. **Open Nine**
2. **Navigate to:** Application → Manage Database
3. **Click:** "Create Manual Backup" in the Backup Actions pane
4. **Wait:** A progress indicator shows backup creation
5. **Confirmation:** A "Backup created successfully" message appears and the new file is listed in **Available Backups**

**Backup details:**

- **Filename:** `Nine_Backup_<Reason>_YYYYMMDD_HHMMSS.db`
  - Example: `Nine_Backup_Manual_20260218_143022.db` (February 18, 2026 at 2:30:22 PM)
- **Location:** `Backups/` folder
  - Linux: `~/.config/Nine/Backups/`
  - Windows: `%APPDATA%\Nine\Backups\`
- **Size:** Same as database file (~5-200 MB)
- **Retention:** Nine keeps the **10 most recent** backups and deletes older ones automatically. Copy backups you want to keep elsewhere (see Method 3).

**Best practice:** Create a manual backup **before**:

- Upgrading to a new version
- Importing large amounts of data
- Making bulk changes (e.g., deleting multiple records)
- Testing new features

### Method 2: Automatic Backups

Nine does **not** run scheduled backups. It creates a backup automatically only at these events:

| Event                       | Filename contains         |
| --------------------------- | ------------------------- |
| Before applying migrations  | `PreMigration_<n>Pending` |
| Initial setup               | `InitialSetup`            |
| Before enabling encryption  | `BeforeEncryption`        |
| Before disabling encryption | `BeforeDecryption`        |
| Before a database reset     | `BeforeReset`             |

To see them, go to **Application → Manage Database** and look in the **Available Backups** list.

### Method 3: External Backup (USB Drive)

For **maximum protection**, copy backups to multiple locations.

**Note:** Nine never uploads your backups. Where you store them (USB drive, external disk, cloud storage service) and who can access that storage is your decision and your responsibility. See [Your Responsibilities](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Privacy.md#your-responsibilities) in the Privacy Statement.

**Within the application (recommended):**

1. **Navigate to:** Application → Manage Database
2. **Click** **Create Manual Backup** in the **Backup Actions** pane, or use an existing backup in the **Available Backups** list.
3. **Click** **Copy To** next to the backup you want to copy.
4. In the Save dialog, choose a location and click **Save**.

**Note:** If the database is encrypted, you need the password to open the backup. You can create an unencrypted copy if needed:

1. **Navigate to:** Application → Manage Database
2. **Click** **Export Unencrypted Backup** in the **Database Encryption** pane.
3. A new `Nine_Unencrypted_Backup_...db` file appears in the **Available Backups** list.
4. **Click** **Copy To** next to it and save it to your chosen location.

**From the command line (USB/external drive):**

**Linux:**

```bash
# Copy all backups to USB drive
cp ~/.config/Nine/Backups/*.db /media/usb/NineBackups/

# Or use rsync for incremental backups
rsync -av ~/.config/Nine/Backups/ /media/usb/NineBackups/
```

**Windows:**

```powershell
# Copy all backups to USB drive at drive letter E:
# Replace E: with the actual drive letter for your USB drive.
Copy-Item "$env:APPDATA\Nine\Backups\*.db" "E:\NineBackups\" -Recurse
```

---

## Restore Procedures

### When to Restore

Restore from backup when:

- Database corruption detected
- Accidental data deletion occurred
- Need to revert to previous state
- Migrating to new computer
- Testing or training scenarios

**Backup from another computer or a USB drive?** On the Manage Database page, click **Upload Backup File** and choose the `.db` file. Nine copies it into the `Backups/` folder and lists it in **Available Backups**, where you can use the methods below. You don't need to copy it into the folder yourself.

### Method 1: Import (Preview Before Committing)

**Import All Data** lets you preview a backup before importing:

**Step-by-step:**

1. **Navigate to:** Application → Manage Database
2. **Locate** the **Available Backups** pane
3. **Choose** a backup from the list
   - Shows filename and date
   - Example: `Nine_Backup_Manual_20260218_143022.db`
4. **Click:** "Preview" button to see the records in the database
5. On the Preview Database page, click the tabs at the bottom to preview the data in each table.
6. **Import:** If the data is correct, click **Import All Data**.
7. Importing the data preserves the data that is already in the existing database that is running **including your log-in information**.

### Method 2: Full Restore (Direct Restore with Restart)

**Full restore** replaces your current database immediately:

**Step-by-step:**

1. **Navigate to:** Application → Manage Database
2. **Find:** "Available Backups" panel
3. **Select** a backup file
4. **Click:** "Restore" button
5. **Confirmation dialog** appears:

   ```
   Are you sure you want to restore from '<backup file name>'?

   This will replace your current database and the application will restart automatically.

   Current database will be saved as .beforeRestore backup.
   ```

6. **Click:** "OK" to proceed (or "Cancel" to abort)
7. **Automatic steps:**
   - The selected backup is staged for restore
   - The application restarts
   - On startup, the current database is renamed to `<database>.beforeRestore.<timestamp>` (in the same folder as the database)
   - The selected backup is moved into place
8. **Verification:** After restart, check your data

**Safety features:**

- ✅ Current database is kept as a `.beforeRestore` file before it is replaced
- ✅ Restore can be undone by renaming that file back to the database name
- ✅ Application restart ensures a clean state

**Important notes:**

- ⚠️ **All users are disconnected** during restart
- ⚠️ **Unsaved work will be lost** - save everything first
- ⚠️ A restore is a **snapshot**: anything entered after the backup was made is not included

### Method 3: Manual Restore (Advanced Users)

If the application won't start or database UI is unavailable:

**Linux:**

```bash
# Stop the application first
pkill Nine

# Navigate to data directory
cd ~/.config/Nine/

# Backup current (corrupted) database
mv app_v1.2.0.db app_v1.2.0.db.corrupted

# Copy backup to main database
cp Backups/Nine_Backup_Manual_20260218_143022.db app_v1.2.0.db

# Restart application
./Nine-1.2.0-x86_64.AppImage
```

**Windows:**

```powershell
# Stop the application first (close window or Task Manager)

# Navigate to data directory
cd $env:APPDATA\Nine

# Backup current (corrupted) database
Move-Item app_v1.2.0.db app_v1.2.0.db.corrupted

# Copy backup to main database
Copy-Item Backups\Nine_Backup_Manual_20260218_143022.db app_v1.2.0.db

# Restart application (Start Menu or double-click icon)
```

**Verify restore was successful:**

1. Application starts without errors
2. Login works normally
3. Data appears correct (check critical records)
4. No error messages in logs

---

## Database Reset

### What is Database Reset?

**Database reset** removes all data and returns the application to **first-run state**:

- All properties, tenants, leases **deleted**
- All financial records **deleted**
- All documents and photos **deleted**
- Organization and user accounts **deleted**
- You will go through New Setup Wizard again

### When to Reset

Reset the database when:

- Starting fresh with new data
- Training or demonstration scenarios
- Troubleshooting requires clean slate
- Development/testing purposes

**⚠️ Warning: Reset deletes all data. Only the automatic safety backup can bring it back.**

### Reset Procedure

**Step-by-step:**

1. **Navigate to:** Application → Manage Database
2. **Click:** "Reset Database" button in the Backup Actions pane (red button)
3. **Confirmation dialog** appears:

   ```
   WARNING: This will delete the current database and create a new blank one. All data will be lost!

   A backup will be created before deletion.

   Are you absolutely sure you want to continue?
   ```

4. **Click:** "OK" to confirm (or "Cancel" to abort)
5. **Automatic steps:**
   - Current database backed up (safety backup: `Nine_Backup_BeforeReset_<timestamp>.db`). If the backup fails, the reset is canceled.
   - Database deleted
   - Application restarts to the home page, where you can create a new account
6. **Setup:** Go through New Account setup to configure the fresh installation

**Safety features:**

- ✅ Automatic backup created before reset
- ✅ Reset is canceled if the backup cannot be created
- ✅ Can restore from the pre-reset backup if needed

**Recovery from reset:**

If you reset by mistake, **immediately restore** from the pre-reset backup:

1. Navigate to Application → Manage Database
2. Find `Nine_Backup_BeforeReset_<timestamp>.db` in **Available Backups**
3. Use the restore procedure (Method 1 or 2 above)
4. Your data will be recovered

---

## Database Health Checks

### Automatic Health Checks

Nine runs an integrity check:

- **On startup** - Before the application loads. If it fails, Nine tries to recover automatically from the most recent valid backup.
- **After migrations** - When a version upgrade applies schema changes.
- **When the Database Settings page opens** - Same check as the manual one below.

You can also run **Auto-Recover from Corruption** from the Backup Actions pane at any time.

### Manual Health Check

**Run a manual health check:**

1. **Navigate to:** Application → Manage Database → Database Health Pane
2. **Click:** "Check Health" button (the check also runs automatically when the page opens)
3. **Wait:** Health check runs (a few seconds)
4. **Results displayed:**

   ```
   ✅ Database Health Check: PASSED

   Database file: app_v1.2.0.db
   Location: /home/user/.config/Nine/app_v1.2.0.db
   File size: 47.3 MB
   Schema version: 1.2.0
   Connection status: Connected
   Integrity check: PASSED

   Record counts:
   - Properties: 9
   - Tenants: 12
   - Leases: 8
   - Invoices: 96
   - Payments: 89

   Last backup: Wednesday, February 18, 2026 2:00 PM
   ```

   **Notes:**
   - **Location** is the full path of the active database file (on Linux typically under `~/.config/Nine/`, on Windows under `%APPDATA%\Nine\`).
   - Record counts are for the active organization only and include records that are not deleted and not archived. The `IsActive` flag is not used.
   - **Last backup** is the newest file in the Backups folder, based on the timestamp in the backup file name (falling back to the file's modified time).

### Health Indicators

**Green indicators (healthy):**

- ✅ **Connection status: Connected** - Database accessible
- ✅ **Integrity check: PASSED** - No corruption detected
- ✅ **Schema version: 1.2.0** - Matches application version

**Yellow indicators (warning):**

- ⚠️ **File size large** - Database over 500 MB (consider cleanup)
- ⚠️ **No recent backup** - Last backup over 7 days ago, or no backups found

**Red indicators (critical):**

- ❌ **Connection failed** - Cannot connect to database
- ❌ **Integrity check: FAILED** - Corruption detected
- ❌ **Schema mismatch** - Version incompatibility
- ❌ **File locked** - Another process using the database (reported as a connection failure)

---

## Troubleshooting

### Issue 1: Database Locked

**Symptoms:**

- Error: "Database is locked"
- Cannot save changes
- Application hangs on database operations

**Causes:**

- Another Nine instance running
- Background backup in progress
- Database file open in SQLite browser tool
- System file lock from crash

**Solutions:**

1. **Check for multiple instances:**

   ```bash
   # Linux
   ps aux | grep Nine

   # Windows (Task Manager)
   # Look for multiple Nine.exe processes
   ```

   - Close extra instances
   - Restart application

2. **Wait for backup to complete:**
   - Check if backup is running (Application → Manage Database)
   - Wait 1-2 minutes for backup to finish

3. **Close database tools:**
   - Close SQLite browser, DB Browser for SQLite, etc.
   - These lock the database file

4. **Reboot computer:**
   - Last resort if file locks persist
   - Clears all locks and processes

5. **Manual lock file removal (advanced):**

   ```bash
   # Only if application is definitely closed
   # Linux
   rm ~/.config/Nine/app_v1.2.0.db-wal
   rm ~/.config/Nine/app_v1.2.0.db-shm

   # Windows
   del %APPDATA%\Nine\app_v1.2.0.db-wal
   del %APPDATA%\Nine\app_v1.2.0.db-shm
   ```

### Issue 2: Database Corruption

**Symptoms:**

- Error: "Database disk image is malformed"
- Application crashes on startup
- Random data disappears
- Integrity check fails

**Causes:**

- Hard drive failure or bad sectors
- System crash during write operation
- Power loss during database update
- SQLite bug (rare)

**Solutions:**

1. **Restore from backup (recommended):**
   - Use **Auto-Recover from Corruption** (Application → Manage Database), or restore the most recent backup manually
   - Follow the restore procedure above
   - Verify data after restore

2. **SQLite recovery tool (if no backup):**

   ```bash
   # Attempt to dump and rebuild database
   sqlite3 app_v1.2.0.db ".dump" | sqlite3 app_v1.2.0_recovered.db

   # Replace corrupted database with recovered one
   mv app_v1.2.0.db app_v1.2.0.db.corrupted
   mv app_v1.2.0_recovered.db app_v1.2.0.db
   ```

3. **Professional data recovery:**
   - If data is critical and no backup exists
   - Contact data recovery specialist
   - May be expensive ($500-$2000)

**Prevention:**

- ✅ Perform backups regularly
- ✅ Store backups off-site (USB drive or cloud storage)
- ✅ Use UPS (uninterruptible power supply)
- ✅ Monitor hard drive health (SMART status)

### Issue 3: Migration Failures

**Symptoms:**

- Application won't start after update
- Error: "Migration failed"
- Schema version mismatch

**Causes:**

- Interrupted migration (crash during update)
- Corrupted migration files
- Schema version conflict

**Solutions:**

1. **Check for error details:**
   - Nine does not currently write a file-based log; review the terminal/console output if you launched it from a terminal
   - Search for "migration" or "schema" in the output
   - Error details help diagnose issue

2. **Rollback to previous version:**
   - Uninstall current version
   - Install previous version
   - Restore backup from before update
   - Contact support for migration help

3. **Manual schema repair (advanced):**
   - Requires SQLite knowledge
   - Export data, fix schema, re-import
   - Not recommended for non-technical users

4. **Fresh install with data export:**
   - Export critical data manually (or via backup)
   - Reset database
   - Reinstall application
   - Manually re-enter data

**Prevention:**

- ✅ **Always backup before updating**
- ✅ Test updates on non-production copy first
- ✅ Wait 1-2 weeks after release before updating (let others test)

### Issue 4: Slow Database Performance

**Symptoms:**

- Slow search results
- Long application startup
- Lagging UI when navigating
- High CPU usage

**Causes:**

- Large database file (>500 MB)
- Many soft-deleted records
- Missing or outdated indexes
- Insufficient RAM

**Solutions:**

1. **Cleanup old data:**
   - Permanently delete soft-deleted records (advanced feature)
   - Archive old financial records
   - Remove unused documents/photos

2. **Check system resources:**
   - Close other applications
   - Increase RAM if below minimum (2 GB)
   - Use SSD instead of HDD for data folder

3. **Split database (advanced):**
   - Not officially supported
   - Contact support for guidance if needed

### Issue 5: Cannot Find Database File

**Symptoms:**

- Error: "Database file not found"
- Application shows empty data
- Fresh installation wizard appears

**Causes:**

- Database file moved or deleted
- Incorrect data folder location
- Permissions issue (Linux)

**Solutions:**

1. **Check default locations:**

   ```bash
   # Linux
   ls -la ~/.config/Nine/

   # Windows
   dir %APPDATA%\Nine
   ```

2. **Search for database file:**

   ```bash
   # Linux
   find ~ -name "app_v1.2.0.db" 2>/dev/null

   # Windows (PowerShell)
   Get-ChildItem -Path C:\ -Filter "app_v1.2.0.db" -Recurse -ErrorAction SilentlyContinue
   ```

3. **Restore from backup:**
   - If original database lost
   - Copy backup to correct location
   - Rename to `app_v1.2.0.db`

4. **Fix permissions (Linux):**
   ```bash
   chmod 644 ~/.config/Nine/app_v1.2.0.db
   chown $USER:$USER ~/.config/Nine/app_v1.2.0.db
   ```

---

## Advanced Topics

### Database File Format

**SQLite version:** 3.x  
**Encoding:** UTF-8  
**Page size:** 4096 bytes  
**Journal mode:** WAL (Write-Ahead Logging)

### WAL Files

You may notice these files alongside the database:

- `app_v1.2.0.db` - Main database file
- `app_v1.2.0.db-wal` - Write-Ahead Log (temporary)
- `app_v1.2.0.db-shm` - Shared Memory file (temporary)

**WAL benefits:**

- Better concurrency (reads don't block writes)
- Faster writes
- Atomic commits

**Important:** When backing up, include **only** `.db` file (not WAL/SHM). Application will recreate WAL files automatically.

### Direct Database Access (Advanced Users)

You can query the database directly using SQLite tools:

**Tools:**

- SQLite command-line: `sqlite3`
- DB Browser for SQLite (GUI)
- SQLiteStudio
- DBeaver

**Example queries:**

```sql
-- View all properties
SELECT * FROM Properties WHERE IsDeleted = 0;

-- Count active leases
SELECT COUNT(*) FROM Leases WHERE Status = 'Active';

-- Total rent collected this year
SELECT SUM(Amount) FROM Payments WHERE PaidOn >= '2026-01-01' AND IsDeleted = 0;

-- Find overdue invoices
SELECT * FROM Invoices WHERE DueOn < date('now') AND Status = 'Pending' AND IsDeleted = 0;
```

**⚠️ Warning:**

- **Read-only** recommended (SELECT queries only)
- **Do NOT** modify data directly (UPDATE, DELETE, INSERT)
- Bypasses application logic and audit trails
- Can cause corruption or data inconsistencies
- Close tool before starting Nine (prevents lock conflicts)

### Data Export

Export data for analysis or migration:

```sql
-- Export properties to CSV
.mode csv
.output properties.csv
SELECT * FROM Properties WHERE IsDeleted = 0;
.output stdout

-- Export financial summary
.output financial_summary.csv
SELECT
  l.PropertyId,
  p.Address,
  COUNT(DISTINCT i.Id) as InvoiceCount,
  SUM(i.Amount) as TotalBilled,
  SUM(i.AmountPaid) as TotalPaid
FROM Leases l
JOIN Properties p ON l.PropertyId = p.Id
LEFT JOIN Invoices i ON i.LeaseId = l.Id
WHERE l.IsDeleted = 0
GROUP BY l.PropertyId, p.Address;
.output stdout
```

### Database Encryption

SQLite databases are **not encrypted** by default. Nine includes database encryption using **SQLCipher**:

**Option 1: Built-in encryption (SQLCipher)**

- **Enable encryption:** Application → Manage Database → Database Encryption pane → **Enable Database Encryption**
- **AES-256 encryption** of the entire database file
- **Master password** required to unlock the database (cached in your system keychain for convenience)
- **Encryption state** tracked in the DatabaseSettings table
- A backup is created automatically before encryption or decryption (`BeforeEncryption` / `BeforeDecryption`)

**Important notes:**

- ⚠️ **Store your password safely** - a lost password means unrecoverable data
- ⚠️ Encrypted backups require the password. Use **Export Unencrypted Backup** if you need a portable copy (see Backup Procedures, Method 3)
- ✅ Transparent to the application once enabled

**Option 2: File-system encryption**

- **Linux:** LUKS encrypted partition or eCryptfs home folder
- **Windows:** BitLocker drive encryption
- **macOS:** FileVault

**Option 3: Third-party encryption tools**

- VeraCrypt (cross-platform encrypted volumes)
- 7-Zip (password-protected archives)

**Recommendation:** Use full-disk encryption (LUKS, BitLocker, FileVault) for simplicity.

### Multi-Computer Sync

Nine is designed as a single-computer desktop application. Multi-computer sync is **not supported**. Do not place the database file in a shared or synced folder.

**⚠️ Risks:**

- Database corruption if two computers write simultaneously
- Sync conflicts if edits are made offline
- Poor performance over a network

**Recommendation:** Use one computer as primary. To move your data to another computer, use a backup and **Upload Backup File** (see the FAQ: "How do I migrate to a new computer?").

### Schema Migrations & Model Optimization

Nine uses **EF Core migrations** to manage schema changes and a **compiled model** to eliminate EF's runtime model-build cost.

#### EF Core Compiled Model

On first startup, EF Core normally reflects over all 40+ entity classes to build its internal model — this adds several seconds to every app launch. A **compiled model** pre-builds this at publish time instead, reducing startup time significantly.

The compiled model lives in `1-Nine.Infrastructure/Data/CompiledModels/` and is auto-discovered via an assembly attribute — no code wiring is needed.

**When to regenerate the compiled model:**

| Change                                            | Re-run optimize? |
| ------------------------------------------------- | ---------------- |
| Add / remove entity or property                   | ✅ Yes           |
| Add / modify relationship or index                | ✅ Yes           |
| Rename entity or property                         | ✅ Yes           |
| Data-only migration (seed data, no schema change) | ❌ No            |
| Bug fix with no model changes                     | ❌ No            |

> **Note:** If the compiled model is stale, EF Core throws an `InvalidOperationException` at startup — the app will not silently use wrong metadata.

#### Migration Workflow

Whenever the entity model changes, run both commands from the solution root:

```bash
# 1. Add the migration
dotnet ef migrations add <MigrationName> \
  --project 1-Nine.Infrastructure \
  --startup-project 4-Nine

# 2. Regenerate the compiled model
dotnet ef dbcontext optimize \
  --project 1-Nine.Infrastructure \
  --startup-project 4-Nine \
  --context ApplicationDbContext \
  --output-dir Data/CompiledModels \
  --namespace Nine.Infrastructure.Data.CompiledModels
```

Check in both the migration files and the updated `CompiledModels/` folder together in the same commit.

#### EF Tools Version

Keep the EF CLI tool current with the runtime version:

```bash
dotnet tool update dotnet-ef -g
```

---

## Best Practices

### Daily Operations

1. **Back up regularly**
   - Nine has no automatic backups. Use **Create Manual Backup** (Application → Manage Database)
   - Verify the newest file in **Available Backups** has the date you expect

2. **Monitor database health**
   - Check health status weekly
   - Review any warnings/errors

3. **Save work regularly**
   - Exit normally (don't force quit)

### Weekly Maintenance

1. **Verify backups**
   - Check the `Backups/` folder
   - Verify recent backup files exist
   - Spot-check a backup (staged restore preview)

2. **Review disk space**
   - Ensure adequate free space (500 MB minimum)
   - Clean up if low on space

### Monthly Maintenance

1. **Copy backups off-site**
   - Copy to an external USB drive or upload to cloud storage
   - Test restore from an off-site backup

2. **Review retention**
   - Nine keeps only the 10 most recent backups, so older ones are deleted automatically
   - Copy backups you want to keep long-term to external storage

### Quarterly Maintenance

1. **Test restore procedure**
   - Practice restoring from backup
   - Verify you can recover data if needed

2. **Archive old data (optional)**
   - Export old financial records
   - Permanently delete records that are no longer needed

### Before Major Updates

1. **Create manual backup**
   - Application → Manage Database → Create Manual Backup

2. **Copy backup to safe location**
   - USB drive or another folder on computer

3. **Document current state**
   - Note schema version
   - List critical data (property count, active leases)

4. **Test update on copy (if possible)**
   - Duplicate database
   - Test update on copy first

---

## FAQ

### Q: How often should I back up my database?

**A:**

- **Minimum:** Weekly
- **Recommended:** Daily (manual; Nine has no scheduled backups)
- **Best practice:** Daily backup + an off-site copy weekly

### Q: Where are backups stored?

**A:**

- **Linux:** `~/.config/Nine/Backups/`
- **Windows:** `%APPDATA%\Nine\Backups\`

### Q: Can I restore a backup from an older version?

**A:** Generally yes, if schema versions are compatible. The application will attempt to migrate the backup forward. However, backups from much older versions (e.g., v0.1.0 → v1.0.0) may not work. Always test staged restore first.

### Q: What if I lose all my backups?

**A:** If both the main database and all backups are lost, **data cannot be recovered** unless you have off-site backups (USB drive, cloud storage). This is why the 3-2-1 backup rule is critical.

### Q: Can I access the database while Nine is running?

**A:** For **read-only** queries with SQLite tools: YES (WAL mode allows concurrent reads). For **write operations**: NO (will cause database lock conflicts).

### Q: How do I migrate to a new computer?

**A:**

1. **Old computer:** Application → Manage Database → **Create Manual Backup**, then **Copy To** next to the new backup and save it to a USB drive or cloud storage.
2. **New computer:** Install Nine.
3. Go to Application → Manage Database and click **Upload Backup File**, then choose the `.db` file (500 MB maximum). Nine copies it into the `Backups/` folder and lists it in **Available Backups** as `Nine_Backup_Uploaded_…`. You don't need to copy files into the folder yourself.
4. Choose how to bring the data in:
   - **Restore** - Replaces the new computer's database with the backup, including user accounts. Best for a full migration.
   - **Preview → Import All Data** - Brings the records into the existing database and keeps its current accounts, including your log-in.

**Note:** If the database is encrypted, you need the password on the new computer. Alternatively, use **Export Unencrypted Backup** on the old computer (see Backup Procedures, Method 3).

### Q: Can I use my database on both Windows and Linux?

**A:** YES! SQLite databases are cross-platform. Copy the `.db` file between systems freely.

### Q: What happens if my hard drive fails?

**A:** If you have **off-site backups** (USB drive, cloud storage), you can recover your data. If not, data is **permanently lost** (professional data recovery may be possible but expensive and not guaranteed).

### Q: Should I backup the WAL files too?

**A:** NO. Only backup the main `.db` file. WAL and SHM files are temporary and recreated automatically.

### Q: Can I edit the database directly instead of using the application?

**A:** **Not recommended.** Direct edits bypass:

- Validation rules
- Audit trails (CreatedBy, LastModifiedOn)
- Business logic (e.g., property status updates)
- Relationships (foreign keys may break)

Use application UI for all changes.

### Q: How large can the database get?

**A:** SQLite theoretical limit is **281 TB**. Practical limit for Nine: **2-4 GB** (depends on available RAM). A typical small landlord database: **50-200 MB** after 5 years.

### Q: Can I have multiple databases?

**A:** Not directly supported. You can manually swap database files, but only one can be active at a time. For multi-organization use, wait for Nine Professional.

---

## Support

Need help with database management? We're here for you!

**Email:** cisguru@outlook.com  
**GitHub Issues:** [https://github.com/xnodeoncode/Nine/issues](https://github.com/xnodeoncode/Nine/issues)

**When contacting support about database issues, include:**

1. **Exact error message** (copy full text)
2. **Steps to reproduce** (what you were doing when error occurred)
3. **Database size** (from the Database Health pane, Application → Manage Database)
4. **Last successful backup** (date/time)
5. **Recent changes** (upgrades, bulk operations, etc.)
6. **Console output**, if launched from a terminal

---

## Summary

**Key takeaways:**

✅ **Back up regularly** - Your insurance policy  
✅ **Store backups off-site** - USB drive or cloud storage  
✅ **Test restore occasionally** - Practice before you need it  
✅ **Monitor database health** - Catch issues early  
✅ **Backup before major changes** - Always

**Follow the 3-2-1 backup rule:**

- **3** copies of data (original + 2 backups)
- **2** different storage types (local + external)
- **1** off-site backup (for example, an external drive kept elsewhere or cloud storage)

**With good database management practices, your data is safe and your business runs smoothly!** 🏠

---

**Document Version:** 1.2.0  
**Last Updated:** November 16, 2026  
**Author:** CIS Guru with GitHub Copilot
