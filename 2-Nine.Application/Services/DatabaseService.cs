using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace Nine.Application.Services;

/// <summary>
/// Service for managing database initialization and migrations.
/// Handles both business (ApplicationDbContext) and product-specific Identity contexts.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ApplicationDbContext _businessContext;
    private readonly DbContext _identityContext;  // Product-specific (NineDbContext or ProfessionalDbContext)
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        ApplicationDbContext businessContext,
        DbContext identityContext,
        ILogger<DatabaseService> logger)
    {
        _businessContext = businessContext;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Starting database initialization...");

        // EF Core's SqliteMigrationLock retries SQLITE_BUSY infinitely (no timeout, no exception).
        // Any open EF RelationalConnection — including ones left "checked out" by GetPendingMigrationsAsync
        // earlier in the same DI scope — blocks BEGIN EXCLUSIVE forever.
        // CloseConnection() forces EF to release its held connection back to the pool.
        // ClearAllPools() then closes the now-pooled connections so SQLite fully releases its file lock.
        _identityContext.Database.CloseConnection();
        _businessContext.Database.CloseConnection();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // ── Era detection and bridge ──────────────────────────────────────
        // Determines how far behind the current schema the on-disk database is
        // and applies the appropriate bridge, or throws if it is too old to bridge.
        await _businessContext.Database.OpenConnectionAsync();
        var era = await DetectEraAsync(_businessContext.Database.GetDbConnection());
        _businessContext.Database.CloseConnection();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        switch (era)
        {
            case Era.NotSupported:
            {
                string backupPath = await BackupUnsupportedDatabaseAsync();
                _logger.LogError(
                    "Database schema is outside the supported upgrade window and cannot be bridged. " +
                    "Backed up to {BackupPath}.", backupPath);
                throw new Nine.Core.Exceptions.DatabaseExceptions.SchemaNotSupportedException(backupPath);
            }

            case Era.SecondAncestor:
                _logger.LogWarning("Second-ancestor era database detected. Applying bridge...");
                await ApplySecondAncestorBridgeAsync();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                break;

            case Era.FirstAncestor:
                _logger.LogWarning("First-ancestor era database detected. Applying bridge...");
                await ApplyFirstAncestorBridgeAsync();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                break;

            // Era.Fresh and Era.Current: no bridge needed — MigrateAsync handles it.
        }

        _logger.LogInformation("Applying identity migrations...");
        await _identityContext.Database.MigrateAsync();
        _logger.LogInformation("Identity migrations applied.");

        // Close identity connection before migrating the business DB — they may share the same file.
        _identityContext.Database.CloseConnection();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        _logger.LogInformation("Applying business migrations...");
        await _businessContext.Database.MigrateAsync();

        _logger.LogInformation("Database initialization complete.");
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            var businessCanConnect = await _businessContext.Database.CanConnectAsync();
            var identityCanConnect = await _identityContext.Database.CanConnectAsync();
            
            return businessCanConnect && identityCanConnect;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database");
            return false;
        }
    }

    public async Task<int> GetPendingMigrationsCountAsync()
    {
        var pending = await _businessContext.Database.GetPendingMigrationsAsync();
        _logger.LogInformation($"Business context has {pending.Count()} pending migrations.");
        return pending.Count();
    }

    public async Task<int> GetIdentityPendingMigrationsCountAsync()
    {
        var pending = await _identityContext.Database.GetPendingMigrationsAsync();
        _logger.LogInformation($"Identity context has {pending.Count()} pending migrations.");
        return pending.Count();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Era bridges
    // Supports the current era and up to two generations back.
    // Databases more than two generations old cannot be bridged — the user is
    // directed to import their data instead.
    //
    // At each major release that squashes migrations, update:
    //   • CurrentEraMarker       → the first migration ID in the new chain
    //   • FirstAncestorMarker    → what was CurrentEraMarker in the previous release
    //   • SecondAncestorMarker   → what was FirstAncestorMarker in the previous release
    //   • ApplyFirstAncestorBridgeAsync()  → new bridge SQL (previous era → current)
    //   • ApplySecondAncestorBridgeAsync() → previous ApplyFirstAncestorBridgeAsync SQL
    // ──────────────────────────────────────────────────────────────────────

    private enum Era { Fresh, Current, FirstAncestor, SecondAncestor, NotSupported }

    /// <summary>
    /// Inspects <c>__EFMigrationsHistory</c> and classifies the on-disk database
    /// into a known era.  The connection must already be open (opened via EF so
    /// the <c>SqlCipherConnectionInterceptor</c> has applied PRAGMA key if needed).
    /// </summary>
    private async Task<Era> DetectEraAsync(DbConnection conn)
    {
        // Known era fingerprints — update at each major squash release.
        const string CurrentEraMarker     = "20260128153724_v1_0_0_InitialCreate"; // Nine v1.x
        const string FirstAncestorMarker  = "20260106195859_InitialCreate";        // Aquiis v1.0.0+
        const string SecondAncestorMarker = "";                                    // TODO: set at next major squash

        // Fresh install — migrations table does not exist yet.
        long tableExists;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master " +
                "WHERE type='table' AND name='__EFMigrationsHistory'";
            tableExists = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        if (tableExists == 0)
            return Era.Fresh;

        // Empty table is treated as fresh (should not occur in practice).
        long rowCount;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
            rowCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        if (rowCount == 0)
            return Era.Fresh;

        async Task<bool> HasMarker(string id)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = id;
            cmd.Parameters.Add(p);
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
        }

        if (!string.IsNullOrEmpty(SecondAncestorMarker) && await HasMarker(SecondAncestorMarker))
            return Era.SecondAncestor;

        if (await HasMarker(FirstAncestorMarker))
            return Era.FirstAncestor;

        if (await HasMarker(CurrentEraMarker))
            return Era.Current;

        // History exists with rows but no known marker — not a supported schema version.
        return Era.NotSupported;
    }

    /// <summary>
    /// Bridges a first-ancestor era (Aquiis v0.3.0 / v1.0.0 / v1.1.0) database
    /// directly to the current Nine era so that EF Core's <see cref="MigrateAsync"/>
    /// can complete normally.  Called only after <see cref="DetectEraAsync"/> confirms
    /// the database is in the first-ancestor era.
    ///
    /// UPDATE THIS METHOD at the next major squash: move its SQL into
    /// <see cref="ApplySecondAncestorBridgeAsync"/> and replace with the new
    /// first-ancestor → current era SQL.
    ///
    /// The entire bridge runs in a single SQLite transaction — either every change
    /// commits or the database is rolled back so the next startup can retry.
    /// </summary>
    private async Task ApplyFirstAncestorBridgeAsync()
    {
        // Open through EF so the SqlCipherConnectionInterceptor (if registered)
        // applies PRAGMA key before any raw SQL executes on this connection.
        await _businessContext.Database.OpenConnectionAsync();
        var conn = _businessContext.Database.GetDbConnection();

        // All bridge statements execute inside a single transaction.
        // If anything fails the database is rolled back so the next startup
        // can retry cleanly.
        var bridgeSql = new[]
        {
            // ── Step 1: Repair migration history ────────────────────────────
            // Remove the old pre-squash identity marker.
            "DELETE FROM \"__EFMigrationsHistory\" " +
                "WHERE \"MigrationId\" = '20260106195859_InitialCreate'",

            // Register the current-era identity InitialCreate.
            // The identity tables already exist in this single-file DB; we only
            // need the history entry so EF sees zero pending identity migrations.
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                "VALUES ('20260104205913_InitialCreate', '10.0.1')",

            // ── Step 2: AddIsSampleDataFlag – fix Invoice/Payment indexes ───
            // Drop the v0.3.0-era single-column Invoice unique index and the
            // bare OrganizationId indexes; replace them with composite ones
            // that are multi-tenant–safe.
            "DROP INDEX IF EXISTS \"IX_Invoices_InvoiceNumber\"",
            "DROP INDEX IF EXISTS \"IX_Invoices_OrganizationId\"",
            "DROP INDEX IF EXISTS \"IX_Payments_OrganizationId\"",
            "CREATE UNIQUE INDEX \"IX_Invoice_OrgId_InvoiceNumber\" " +
                "ON \"Invoices\" (\"OrganizationId\", \"InvoiceNumber\")",
            "CREATE UNIQUE INDEX \"IX_Payment_OrgId_PaymentNumber\" " +
                "ON \"Payments\" (\"OrganizationId\", \"PaymentNumber\")",

            // ── Step 3: AddIsSampleDataFlag – add IsSampleData column ───────
            "ALTER TABLE \"ApplicationScreenings\"      ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"CalendarEvents\"             ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"CalendarSettings\"           ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"ChecklistItems\"             ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Checklists\"                 ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"ChecklistTemplateItems\"     ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"ChecklistTemplates\"         ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Documents\"                  ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Inspections\"                ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Invoices\"                   ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"LeaseOffers\"                ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Leases\"                     ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"MaintenanceRequests\"        ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Notes\"                      ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"NotificationPreferences\"    ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Notifications\"              ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"OrganizationEmailSettings\"  ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"OrganizationSettings\"       ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"OrganizationSMSSettings\"    ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Payments\"                   ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Properties\"                 ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"ProspectiveTenants\"         ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"RentalApplications\"         ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Repairs\"                    ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"SecurityDepositDividends\"   ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"SecurityDepositInvestmentPools\" ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"SecurityDeposits\"           ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Tenants\"                    ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Tours\"                      ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"UserProfiles\"               ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"WorkflowAuditLogs\"          ADD COLUMN \"IsSampleData\" INTEGER NOT NULL DEFAULT 0",

            // ── Step 4: UpdateExistingSampleDataFlag – tag system-seeded rows ─
            "UPDATE \"Properties\" SET \"IsSampleData\" = 1 WHERE \"CreatedBy\" = '00000000-0000-0000-0000-000000000001'",
            "UPDATE \"Tenants\"    SET \"IsSampleData\" = 1 WHERE \"CreatedBy\" = '00000000-0000-0000-0000-000000000001'",
            "UPDATE \"Leases\"     SET \"IsSampleData\" = 1 WHERE \"CreatedBy\" = '00000000-0000-0000-0000-000000000001'",
            "UPDATE \"Invoices\"   SET \"IsSampleData\" = 1 WHERE \"CreatedBy\" = '00000000-0000-0000-0000-000000000001'",
            "UPDATE \"Payments\"   SET \"IsSampleData\" = 1 WHERE \"CreatedBy\" = '00000000-0000-0000-0000-000000000001'",

            // ── Step 5: ConsolidateOrganizationIdToBaseModel — no schema change ─
            //    (code-only refactor; nothing to execute)

            // ── Step 6: RenameIsAvailableToIsActive ──────────────────────────
            "ALTER TABLE \"Properties\" RENAME COLUMN \"IsAvailable\" TO \"IsActive\"",

            // ── Step 7: Record all just-applied migrations ───────────────────
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                "VALUES ('20260212163628_AddIsSampleDataFlag', '10.0.1')",
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                "VALUES ('20260212165047_UpdateExistingSampleDataFlag', '10.0.1')",
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                "VALUES ('20260216205819_ConsolidateOrganizationIdToBaseModel', '10.0.1')",
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                "VALUES ('20260313122831_RenameIsAvailableToIsActive', '10.0.1')",
        };

        using var transaction = conn.BeginTransaction();
        try
        {
            foreach (var sql in bridgeSql)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
            _logger.LogInformation(
                "First-ancestor era bridge applied. Database is now on the current era.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "First-ancestor bridge failed — rolling back to preserve database integrity.");
            transaction.Rollback();
            throw;
        }
        finally
        {
            _businessContext.Database.CloseConnection();
        }
    }

    /// <summary>
    /// Bridges a second-ancestor era database directly to the current era.
    /// Called only after <see cref="DetectEraAsync"/> confirms the database is
    /// in the second-ancestor era.
    ///
    /// NOT YET IMPLEMENTED — no second-ancestor era exists at this release.
    /// Implement at the next major squash by moving the current
    /// <see cref="ApplyFirstAncestorBridgeAsync"/> SQL here and writing new
    /// first-ancestor → current bridge SQL above.
    /// </summary>
    private Task ApplySecondAncestorBridgeAsync()
    {
        // SecondAncestorMarker in DetectEraAsync is intentionally empty so this
        // method is never reachable in practice until the next major squash.
        throw new NotImplementedException(
            "ApplySecondAncestorBridgeAsync is not yet implemented. " +
            "Populate SecondAncestorMarker in DetectEraAsync and add bridge SQL here " +
            "at the next major squash release.");
    }

    /// <summary>
    /// Copies the business database file to the Backups folder before a
    /// <see cref="Nine.Core.Exceptions.DatabaseExceptions.SchemaNotSupportedException"/> is thrown.
    /// Returns the full path of the backup file, or an empty string if the
    /// source file could not be located.
    /// </summary>
    private Task<string> BackupUnsupportedDatabaseAsync()
    {
        var connStr = _businessContext.Database.GetConnectionString() ?? string.Empty;
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connStr);
        var dbPath = builder.DataSource;

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            _logger.LogWarning("Could not locate database file for backup (path: {DbPath}).", dbPath);
            return Task.FromResult(string.Empty);
        }

        var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".";
        var backupDir = Path.GetFullPath(Path.Combine(dbDir, "Backups"));
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupName =
            $"unsupported_{Path.GetFileNameWithoutExtension(dbPath)}_{timestamp}{Path.GetExtension(dbPath)}";
        var backupPath = Path.Combine(backupDir, backupName);

        File.Copy(dbPath, backupPath, overwrite: false);
        _logger.LogInformation("Database backed up to {BackupPath}.", backupPath);
        return Task.FromResult(backupPath);
    }

    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the database settings (creates default if not exists)
    /// </summary>
    public async Task<Nine.Core.Entities.DatabaseSettings> GetDatabaseSettingsAsync()
    {
        var settings = await _businessContext.DatabaseSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();

        if (settings == null)
        {
            // Create default settings
            settings = new Nine.Core.Entities.DatabaseSettings
            {
                DatabaseEncryptionEnabled = false,
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System"
            };

            _businessContext.DatabaseSettings.Add(settings);
            await _businessContext.SaveChangesAsync();

            _logger.LogInformation("Created default database settings");
        }

        return settings;
    }

    /// <summary>
    /// Updates database encryption status
    /// </summary>
    public async Task SetDatabaseEncryptionAsync(bool enabled, string modifiedBy = "System")
    {
        var settings = await GetDatabaseSettingsAsync();
        settings.DatabaseEncryptionEnabled = enabled;
        settings.EncryptionChangedOn = DateTime.UtcNow;
        settings.LastModifiedOn = DateTime.UtcNow;
        settings.LastModifiedBy = modifiedBy;

        _businessContext.DatabaseSettings.Update(settings);
        await _businessContext.SaveChangesAsync();

        _logger.LogInformation("Database encryption {Status} by {ModifiedBy}", 
            enabled ? "enabled" : "disabled", modifiedBy);
    }

    /// <summary>
    /// Gets current database encryption status
    /// </summary>
    public async Task<bool> IsDatabaseEncryptionEnabledAsync()
    {
        var settings = await GetDatabaseSettingsAsync();
        return settings.DatabaseEncryptionEnabled;
    }
}

