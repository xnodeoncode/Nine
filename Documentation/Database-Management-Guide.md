# Nine - Database Management Guide

**Version:** 1.1.2  
**Last Updated:** March 1, 2026  
**Audience:** Administrators and Power Users

---

## 📖 Table of Contents

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

## 📋 Overview

Nine uses **SQLite** as its database engine - a lightweight, file-based database that requires no server installation or configuration. This guide covers everything you need to know about managing your database, protecting your data, and recovering from issues.

### What You'll Learn

- ✅ How to back up your database (manual and automatic)
- ✅ How to restore from a backup
- ✅ How to check database health
- ✅ How to troubleshoot common database issues
- ✅ How to optimize database performance
- ✅ Best practices for data protection

### Why Database Management Matters

Your database contains **all your property management data:**

- Properties and tenants
- Leases and financial records
- Invoices and payments
- Maintenance requests and inspections
- Documents and photos
- User accounts and settings

**Regular backups** are your insurance policy against:

- Hardware failure (hard drive crash, SSD failure)
- Software bugs or corruption
- Accidental data deletion
- Ransomware or malware
- Natural disasters or theft

---

## 💾 Database Location & Structure

### Database File Location

**Linux:**

```bash
~/.config/Nine/Data/app_v1.1.0.db
```

**Windows:**

```
%APPDATA%\Nine\Data\app_v1.1.0.db
```

**Full path examples:**

- **Linux:** `/home/username/.config/Nine/Data/app_v1.1.0.db`
- **Windows:** `C:\Users\YourName\AppData\Roaming\Nine\Data\app_v1.1.0.db`

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

Nine v1.1.0 uses schema version `1.1.0`. This tracks the database structure and ensures compatibility.

**Check your schema version:**

1. Navigate to **Settings** → **Database**
2. View **"Database Information"** panel
3. See \*\*"Schema Version: 1.1.0""

### Auto-Migration System

When you upgrade Nine to a new version:

1. Application detects current schema version
2. Compares with required schema version
3. Automatically applies migrations if needed
4. Updates schema version in database
5. Application starts normally

**No manual intervention required** - migrations are automatic and seamless!

**Version Upgrade Compatibility:**  
Not all version upgrades are supported automatically. Check the **[Compatibility Matrix](Compatibility-Matrix.md)** before upgrading to see if your current version can auto-migrate to the target version, or if manual steps are required.

#### Migration to v1.1.0

When upgrading from v1.0.0 to v1.1.0, the following schema changes are automatically applied:

> **Note:** For complete version compatibility information and upgrade paths, see the **[Compatibility Matrix](Compatibility-Matrix.md)**.

**New Tables:**

- **DatabaseSettings** - Tracks encryption state and configuration
  - DatabaseEncryptionEnabled (boolean)
  - EncryptionChangedOn (datetime)
  - Other security settings

**New Columns:**

- **IsSampleData** - Boolean flag added to 30+ tables to identify sample/demo data
  - Default value: false
  - Helps distinguish real data from system-generated examples

**Index Changes:**

- **Invoice/Payment unique indexes** - Now composite indexes including OrganizationId
  - Ensures multi-tenant data integrity
  - Prevents number conflicts across organizations

**Other Enhancements:**

- **EncryptionSalt** column added (nullable) for future encryption support

**Migration Notes:**

- All changes are additive (no data loss)
- Migration runs automatically on first startup with v1.1.0
- Backup created automatically before migration
- Typical migration time: 5-15 seconds

**Important:** v1.1.0 application **requires** v1.1.0 database schema - the DatabaseSettings table is queried on startup. Running v1.1.0 app with v1.0.0 database will result in an error.

**Version Compatibility:**  
See the **[Compatibility Matrix](Compatibility-Matrix.md)** for detailed information about which app versions work with which database versions, and whether automatic migration is available.

---

## 🔐 Backup Procedures

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
2. **Navigate to:** Settings → Database
3. **Click:** "Backup & Restore" tab
4. **Click:** "Create Backup" button
5. **Wait:** Progress indicator shows backup creation
6. **Confirmation:** "Backup created successfully!" message appears

**Backup details:**

- **Filename:** `backup_YYYYMMDD_HHMMSS.db`
  - Example: `backup_20260218_143022.db` (February 18, 2026 at 2:30:22 PM)
- **Location:** `Data/Backups/` folder
  - Linux: `~/.config/Nine/Data/Backups/`
  - Windows: `%APPDATA%\Nine\Data\Backups\`
- **Size:** Same as database file (~5-200 MB)

**Best practice:** Create a manual backup **before**:

- Upgrading to a new version
- Importing large amounts of data
- Making bulk changes (e.g., deleting multiple records)
- Testing new features

### Method 2: Scheduled Automatic Backups (Recommended for Daily Use)

Set up automatic backups that run on a schedule:

**Configuration:**

1. **Navigate to:** Settings → Database → Backup & Restore
2. **Enable:** "Schedule Automatic Backups" toggle (turn ON)
3. **Choose frequency:**
   - Daily (recommended)
   - Weekly
   - Monthly
4. **Set backup time:**
   - Default: 2:00 AM (when computer is idle)
   - Choose a time when application is running
5. **Set retention policy:**
   - Keep backups for: 7 days, 30 days, 90 days, Forever
   - Older backups automatically deleted to save space
6. **Click:** "Save Settings"

**Important notes:**

- ⚠️ **Computer must be on** for scheduled backups to run
- ⚠️ **Application must be running** (can be minimized)
- ✅ Backups run in background (no interruption to your work)
- ✅ Failed backups logged and retried next scheduled time

**Verify automatic backups are working:**

1. Check the "Last Backup" timestamp in Database settings
2. Browse to `Data/Backups/` folder
3. Verify recent backup files exist
4. Check file dates match your schedule

### Method 3: External Backup (Cloud or USB Drive)

For **maximum protection**, copy backups to multiple locations:

**Cloud storage examples:**

- Google Drive
- Dropbox
- OneDrive
- iCloud Drive
- Backblaze
- AWS S3

**USB/External drive backup:**

**Linux:**

```bash
# Copy all backups to USB drive
cp ~/.config/Nine/Data/Backups/*.db /media/usb/NineBackups/

# Or use rsync for incremental backups
rsync -av ~/.config/Nine/Data/Backups/ /media/usb/NineBackups/
```

**Windows:**

```powershell
# Copy all backups to USB drive
Copy-Item "$env:APPDATA\Nine\Data\Backups\*.db" "E:\NineBackups\" -Recurse
```

**Automated cloud sync:**

Many cloud services offer **folder sync** - configure your cloud client to sync the Backups folder:

1. Install cloud storage client (e.g., Dropbox, Google Drive)
2. Configure sync for: `Data/Backups/` folder
3. Backups automatically uploaded to cloud
4. Access your backups from anywhere

**Best practice:** Use the **3-2-1 backup rule**:

- **3** copies of your data (original + 2 backups)
- **2** different storage types (local SSD + external USB)
- **1** off-site backup (cloud storage)

---

## 🔄 Restore Procedures

### When to Restore

Restore from backup when:

- Database corruption detected
- Accidental data deletion occurred
- Need to revert to previous state
- Migrating to new computer
- Testing or training scenarios

### Method 1: Staged Restore (Preview Before Committing)

**Staged restore** lets you preview a backup before fully restoring it:

**Step-by-step:**

1. **Navigate to:** Settings → Database → Backup & Restore
2. **Click:** "Available Backups" dropdown
3. **Select** a backup from the list:
   - Shows filename and date
   - Example: `backup_20260218_143022.db (Feb 18, 2026 2:30 PM)`
4. **Click:** "Staged Restore" button
5. **Wait:** System creates temporary copy and validates backup
6. **Preview:** Backup information shown:
   - Backup date and time
   - Database size
   - Schema version
   - Record counts (properties, tenants, leases)
7. **Decision point:**
   - If backup looks correct: **Click "Confirm Restore"**
   - If backup is wrong: **Click "Cancel"** (no changes made)

**What happens during staged restore:**

1. Backup file copied to staging area (`Data/Staging/`)
2. Backup validated (integrity check)
3. Schema version verified
4. Record counts displayed for review
5. Original database **NOT modified** until you confirm

**Advantages:**

- ✅ Safe - preview before committing
- ✅ Verify backup integrity before restore
- ✅ See what data will be restored
- ✅ Can cancel without risk

### Method 2: Full Restore (Direct Restore with Restart)

**Full restore** replaces your current database immediately:

**Step-by-step:**

1. **Navigate to:** Settings → Database → Backup & Restore
2. **Click:** "Available Backups" dropdown
3. **Select** a backup file
4. **Click:** "Restore from Backup" button
5. **Confirmation dialog** appears:

   ```
   ⚠️ Warning: This will replace your current database

   Current database will be backed up automatically before restore.
   Application will restart after restore completes.

   Are you sure you want to continue?
   ```

6. **Click:** "Yes, Restore" to proceed (or "Cancel" to abort)
7. **Automatic steps:**
   - Current database backed up (safety backup)
   - Selected backup validated
   - Current database replaced with backup
   - Application restarts automatically
8. **Verification:** After restart, check data to ensure correct

**Safety features:**

- ✅ Current database automatically backed up before restore
- ✅ Restore can be undone (restore the safety backup)
- ✅ Validation prevents corrupted backup from being used
- ✅ Application restart ensures clean state

**Important notes:**

- ⚠️ **All users logged out** during restore
- ⚠️ **Unsaved work will be lost** - save everything first
- ⚠️ **Takes 10-30 seconds** depending on database size

### Method 3: Manual Restore (Advanced Users)

If the application won't start or database UI is unavailable:

**Linux:**

```bash
# Stop the application first
pkill Nine

# Navigate to data directory
cd ~/.config/Nine/Data/

# Backup current (corrupted) database
mv app_v1.1.0.db app_v1.1.0.db.corrupted

# Copy backup to main database
cp Backups/backup_20260218_143022.db app_v1.1.0.db

# Restart application
./Nine-1.1.0.AppImage
```

**Windows:**

```powershell
# Stop the application first (close window or Task Manager)

# Navigate to data directory
cd $env:APPDATA\Nine\Data

# Backup current (corrupted) database
Move-Item app_v1.1.0.db app_v1.1.0.db.corrupted

# Copy backup to main database
Copy-Item Backups\backup_20260218_143022.db app_v1.1.0.db

# Restart application (Start Menu or double-click icon)
```

**Verify restore was successful:**

1. Application starts without errors
2. Login works normally
3. Data appears correct (check critical records)
4. No error messages in logs

---

## 🔄 Database Reset

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
- Selling/transferring application to someone else
- Troubleshooting requires clean slate
- Development/testing purposes

**⚠️ Warning: Reset is PERMANENT and cannot be undone!**

### Reset Procedure

**Step-by-step:**

1. **Navigate to:** Settings → Database → Backup & Restore
2. **Click:** "Advanced" tab
3. **Click:** "Reset Database" button (red button at bottom)
4. **Confirmation dialog** appears:

   ```
   ⚠️ DANGER: Reset Database

   This will DELETE ALL DATA including:
   - All properties, tenants, and leases
   - All invoices, payments, and financial records
   - All maintenance requests and inspections
   - All documents and photos
   - Organization and user accounts

   A backup will be created automatically before reset.

   Type "RESET" to confirm (case-sensitive):
   _______
   ```

5. **Type:** `RESET` (exactly, uppercase)
6. **Click:** "Confirm Reset"
7. **Automatic steps:**
   - Current database backed up (safety backup: `pre-reset-backup_20260218_DATE.db`)
   - Database reset to empty schema
   - Application restarts to New Setup Wizard
8. **Setup:** Go through New Setup Wizard to configure fresh installation

**Safety features:**

- ✅ Automatic backup created before reset
- ✅ Typed confirmation required (prevents accidental clicks)
- ✅ Can restore from pre-reset backup if needed

**Recovery from reset:**

If you reset by mistake, **immediately restore** from the pre-reset backup:

1. Navigate to Backups folder
2. Find: `pre-reset-backup_20260218_143022.db`
3. Use restore procedure (Method 2 or 3 above)
4. Your data will be recovered

---

## 🏥 Database Health Checks

### Automatic Health Monitoring

Nine monitors database health continuously:

- **On startup** - Integrity check before application loads
- **Every hour** - Background health check via scheduled tasks
- **Before backup** - Verify database is healthy before backup
- **After errors** - Check health if database errors occur

### Manual Health Check

**Run a manual health check:**

1. **Navigate to:** Settings → Database → Health & Monitoring
2. **Click:** "Run Health Check" button
3. **Wait:** Health check runs (5-15 seconds)
4. **Results displayed:**

   ```
   ✅ Database Health Check: PASSED

   Database file: app_v1.1.0.db
   File size: 47.3 MB
   Schema version: 1.1.0
   Connection status: Connected
   Integrity check: PASSED

   Record counts:
   - Properties: 9
   - Tenants: 12
   - Leases: 8
   - Invoices: 96
   - Payments: 89

   Last backup: February 18, 2026 2:00 AM
   Next scheduled backup: February 19, 2026 2:00 AM
   ```

### Health Indicators

**Green indicators (healthy):**

- ✅ **Connection status: Connected** - Database accessible
- ✅ **Integrity check: PASSED** - No corruption detected
- ✅ **Schema version: 1.1.0** - Matches application version
- ✅ **File accessible: Yes** - Database file readable and writable

**Yellow indicators (warning):**

- ⚠️ **File size large** - Database over 500 MB (consider cleanup)
- ⚠️ **No recent backup** - Last backup over 7 days ago
- ⚠️ **Slow queries** - Database performance degraded

**Red indicators (critical):**

- ❌ **Connection failed** - Cannot connect to database
- ❌ **Integrity check: FAILED** - Corruption detected
- ❌ **Schema mismatch** - Version incompatibility
- ❌ **File locked** - Another process using database

### Database Optimization

**When to optimize:**

- Database feels slow
- Search queries take long time
- Application startup is slow
- After bulk data operations (imports, deletions)

**Run optimization:**

1. **Navigate to:** Settings → Database → Health & Monitoring
2. **Click:** "Optimize Database" button
3. **Wait:** Optimization runs (30 seconds to 2 minutes)
4. **Automatic steps:**
   - VACUUM command (reclaim unused space)
   - ANALYZE command (update statistics)
   - Index rebuilding
   - Cache refresh
5. **Results:** Performance improvements and size reduction

**Expected results:**

- **Speed:** Queries 20-50% faster
- **Size:** Database file 10-30% smaller
- **Startup:** Application launches faster

**Recommendation:** Optimize database quarterly (every 3 months)

---

## 🔧 Troubleshooting

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
   - Check if backup is running (Settings → Database)
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
   rm ~/.config/Nine/Data/app_v1.1.0.db-wal
   rm ~/.config/Nine/Data/app_v1.1.0.db-shm

   # Windows
   del %APPDATA%\Nine\Data\app_v1.1.0.db-wal
   del %APPDATA%\Nine\Data\app_v1.1.0.db-shm
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
   - Use most recent backup
   - Follow restore procedure above
   - Verify data after restore

2. **SQLite recovery tool (if no backup):**

   ```bash
   # Attempt to dump and rebuild database
   sqlite3 app_v1.1.0.db ".dump" | sqlite3 app_v1.1.0_recovered.db

   # Replace corrupted database with recovered one
   mv app_v1.1.0.db app_v1.1.0.db.corrupted
   mv app_v1.1.0_recovered.db app_v1.1.0.db
   ```

3. **Professional data recovery:**
   - If data is critical and no backup exists
   - Contact data recovery specialist
   - May be expensive ($500-$2000)

**Prevention:**

- ✅ Enable automatic daily backups
- ✅ Store backups off-site (cloud or USB)
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

1. **Check logs:**
   - Navigate to: `Data/Logs/`
   - Open most recent log file
   - Search for "migration" or "schema"
   - Error details help diagnose issue

2. **Rollback to previous version:**
   - Uninstall current version
   - Install previous version
   - Restore backup from before update
   - Contact support for migration help

3. **Manual schema repair (advanced):**
   - Requires SQLite knowledge
   - Export data, fix schema, reimport
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

1. **Optimize database:**
   - Settings → Database → Optimize
   - Rebuilds indexes and reclaims space

2. **Cleanup old data:**
   - Permanently delete soft-deleted records (advanced feature)
   - Archive old financial records
   - Remove unused documents/photos

3. **Check system resources:**
   - Close other applications
   - Increase RAM if below minimum (2 GB)
   - Use SSD instead of HDD for data folder

4. **Split database (advanced):**
   - Not officially supported in v1.0.0
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
   ls -la ~/.config/Nine/Data/

   # Windows
   dir %APPDATA%\Nine\Data
   ```

2. **Search for database file:**

   ```bash
   # Linux
   find ~ -name "app_v1.1.0.db" 2>/dev/null

   # Windows (PowerShell)
   Get-ChildItem -Path C:\ -Filter "app_v1.1.0.db" -Recurse -ErrorAction SilentlyContinue
   ```

3. **Restore from backup:**
   - If original database lost
   - Copy backup to correct location
   - Rename to `app_v1.1.0.db`

4. **Fix permissions (Linux):**
   ```bash
   chmod 644 ~/.config/Nine/Data/app_v1.1.0.db
   chown $USER:$USER ~/.config/Nine/Data/app_v1.1.0.db
   ```

---

## 🎓 Advanced Topics

### Database File Format

**SQLite version:** 3.x  
**Encoding:** UTF-8  
**Page size:** 4096 bytes  
**Journal mode:** WAL (Write-Ahead Logging)

### WAL Files

You may notice these files alongside the database:

- `app_v1.1.0.db` - Main database file
- `app_v1.1.0.db-wal` - Write-Ahead Log (temporary)
- `app_v1.1.0.db-shm` - Shared Memory file (temporary)

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

-- Total rent collected this month
SELECT SUM(Amount) FROM Payments WHERE PaymentDate >= '2026-01-01';

-- Find overdue invoices
SELECT * FROM Invoices WHERE DueDate < date('now') AND Status = 'Pending';
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
  p.PropertyName,
  COUNT(DISTINCT i.Id) as InvoiceCount,
  SUM(i.Amount) as TotalBilled,
  SUM(i.AmountPaid) as TotalPaid
FROM Leases l
JOIN Properties p ON l.PropertyId = p.Id
LEFT JOIN Invoices i ON i.LeaseId = l.Id
WHERE l.IsDeleted = 0
GROUP BY l.PropertyId, p.PropertyName;
.output stdout
```

### Database Encryption

SQLite databases are **not encrypted** by default. Nine v1.1.0 includes database encryption options:

**Option 1: SQLCipher (Included in v1.1.0)**

SQLCipher provides AES-256 encryption for database files. This is integrated into Nine v1.1.0:

- **Enable encryption:** Settings → Database → Security → Enable Database Encryption
- **AES-256 encryption** of entire database file
- **Master password** required to unlock database
- **Encryption state** tracked in DatabaseSettings table
- **Performance impact:** Minimal (2-5% overhead)

**Important notes:**

- ⚠️ **Backup before enabling encryption** (encrypted backups require password)
- ⚠️ **Store password safely** - lost password = unrecoverable data
- ✅ Encryption applied to new data immediately
- ✅ Existing data encrypted in background (may take several minutes)
- ✅ Transparent to application once enabled (automatic encryption/decryption)

**Option 2: File-system encryption**

- **Linux:** LUKS encrypted partition or eCryptfs home folder
- **Windows:** BitLocker drive encryption
- **macOS:** FileVault

**Option 3: Third-party encryption tools**

- VeraCrypt (cross-platform encrypted volumes)
- 7-Zip (password-protected archives)

**Recommendation:** Use full-disk encryption (BitLocker, FileVault) for simplicity.

### Multi-Computer Sync

Nine is designed as single-computer desktop application. Multi-computer sync is **not officially supported** but possible with caveats:

**Cloud sync approach:**

1. Place database file in synced folder (Dropbox, Google Drive)
2. Configure symlink to synced location
3. Only use on **one computer at a time** (avoid conflicts)

**⚠️ Risks:**

- Database corruption if both computers write simultaneously
- Sync conflicts if edits made offline
- Poor performance over network

**Recommendation:** Use one computer as primary. For multi-computer access, wait for Nine Professional (web-based, v2.0.0).

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

## 📌 Best Practices

### Daily Operations

1. **Enable automatic backups**
   - Set to daily at 2 AM
   - Verify backups run successfully (check Last Backup timestamp)

2. **Monitor database health**
   - Check health status weekly
   - Review any warnings/errors

3. **Save work regularly**
   - Application auto-saves most changes
   - Exit normally (don't force quit)

### Weekly Maintenance

1. **Verify backups**
   - Check `Data/Backups/` folder
   - Verify recent backup files exist
   - Spot-check a backup (staged restore preview)

2. **Review disk space**
   - Ensure adequate free space (500 MB minimum)
   - Clean up if low on space

### Monthly Maintenance

1. **Copy backups off-site**
   - Upload to cloud storage
   - Copy to external USB drive
   - Test restore from off-site backup

2. **Review retention policy**
   - Delete very old backups if needed
   - Keep at least 3 months of backups

### Quarterly Maintenance

1. **Optimize database**
   - Settings → Database → Optimize
   - Improves performance

2. **Test restore procedure**
   - Practice restoring from backup
   - Verify you can recover data if needed

3. **Archive old data (optional)**
   - Export old financial records
   - Permanently delete soft-deleted records (if feature available)

### Before Major Updates

1. **Create manual backup**
   - Settings → Database → Create Backup

2. **Copy backup to safe location**
   - USB drive or cloud storage

3. **Document current state**
   - Note schema version
   - List critical data (property count, active leases)

4. **Test update on copy (if possible)**
   - Duplicate database
   - Test update on copy first

---

## ❓ FAQ

### Q: How often should I back up my database?

**A:**

- **Minimum:** Weekly
- **Recommended:** Daily (automatic)
- **Best practice:** Daily automatic + off-site copy weekly

### Q: Where are backups stored?

**A:**

- **Linux:** `~/.config/Nine/Data/Backups/`
- **Windows:** `%APPDATA%\Nine\Data\Backups\`

### Q: Can I restore a backup from an older version?

**A:** Generally yes, if schema versions are compatible. The application will attempt to migrate the backup forward. However, backups from much older versions (e.g., v0.1.0 → v1.1.0) may not work. Always test staged restore first.

### Q: What if I lose all my backups?

**A:** If both the main database and all backups are lost, **data cannot be recovered** unless you have off-site backups (cloud, USB drive). This is why the 3-2-1 backup rule is critical.

### Q: Can I access the database while Nine is running?

**A:** For **read-only** queries with SQLite tools: YES (WAL mode allows concurrent reads). For **write operations**: NO (will cause database lock conflicts).

### Q: How do I migrate to a new computer?

**A:**

1. On old computer: Create backup
2. Copy backup file to new computer (USB drive, cloud)
3. Install Nine on new computer
4. Copy backup to: `Data/Backups/` folder on new computer
5. Restore from backup

Or simply copy the entire `Data/` folder to new computer.

### Q: Can I use my database on both Windows and Linux?

**A:** YES! SQLite databases are cross-platform. Copy the `.db` file between systems freely.

### Q: What happens if my hard drive fails?

**A:** If you have **off-site backups** (cloud, USB), you can recover your data. If not, data is **permanently lost** (professional data recovery may be possible but expensive and not guaranteed).

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

**A:** Not directly supported in v1.1.0. You can manually swap database files, but only one can be active at a time. For multi-organization use, wait for Nine Professional.

---

## 📞 Support

Need help with database management? We're here for you!

**Email:** cisguru@outlook.com  
**GitHub Issues:** [https://github.com/xnodeoncode/nine/issues](https://github.com/xnodeoncode/nine/issues)

**When contacting support about database issues, include:**

1. **Exact error message** (copy full text)
2. **Steps to reproduce** (what you were doing when error occurred)
3. **Database size** (from Settings → Database)
4. **Last successful backup** (date/time)
5. **Recent changes** (upgrades, bulk operations, etc.)
6. **Log files** (Settings → System → Export Logs)

---

## 🎓 Summary

**Key takeaways:**

✅ **Enable automatic daily backups** - Your insurance policy  
✅ **Store backups off-site** - Cloud or USB drive  
✅ **Test restore occasionally** - Practice before you need it  
✅ **Monitor database health** - Catch issues early  
✅ **Optimize quarterly** - Maintain performance  
✅ **Backup before major changes** - Always

**Follow the 3-2-1 backup rule:**

- **3** copies of data (original + 2 backups)
- **2** different storage types (local + external)
- **1** off-site backup (cloud or remote location)

**With good database management practices, your data is safe and your business runs smoothly!** 🏠

---

**Document Version:** 1.1  
**Last Updated:** February 18, 2026  
**Author:** CIS Guru with GitHub Copilot
