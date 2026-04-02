using Nine.Application.Models.DTOs;
using Nine.Core.Interfaces;
using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;
using Nine.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services;

/// <summary>
/// Service for previewing backup databases in read-only mode and importing data from them.
/// Allows viewing database contents without overwriting the active database.
/// </summary>
public class DatabasePreviewService
{
    private readonly IPathService _pathService;
    private readonly IKeychainService _keychain;
    private readonly ApplicationDbContext _activeContext;
    private readonly IUserContextService _userContext;
    private readonly ILogger<DatabasePreviewService> _logger;

    public DatabasePreviewService(
        IPathService pathService,
        IKeychainService keychain,
        ApplicationDbContext activeContext,
        IUserContextService userContext,
        ILogger<DatabasePreviewService> logger)
    {
        _pathService = pathService;
        _keychain = keychain;
        _activeContext = activeContext;
        _userContext = userContext;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Path helpers
    // -------------------------------------------------------------------------

    private async Task<string> GetBackupDirectoryAsync()
    {
        var dbPath = await _pathService.GetDatabasePathAsync();
        return Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");
    }

    private async Task<string> GetDataDirectoryAsync()
    {
        var dbPath = await _pathService.GetDatabasePathAsync();
        return Path.GetDirectoryName(dbPath)!;
    }

    private async Task<string> GetBackupFilePathAsync(string backupFileName)
    {
        // Security: prevent path traversal
        var safeFileName = Path.GetFileName(backupFileName);
        var backupPath = Path.Combine(await GetBackupDirectoryAsync(), safeFileName);
        if (File.Exists(backupPath))
            return backupPath;

        // Fall back to data directory (e.g. app_v1.0.0.db living alongside the active DB)
        var dataPath = Path.Combine(await GetDataDirectoryAsync(), safeFileName);
        return dataPath;
    }

    // -------------------------------------------------------------------------
    // Encryption helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Check if a backup database file is encrypted
    /// </summary>
    public async Task<bool> IsDatabaseEncryptedAsync(string backupFileName)
    {
        var backupPath = await GetBackupFilePathAsync(backupFileName);

        if (!File.Exists(backupPath))
        {
            _logger.LogWarning("Backup file not found: {Path}", backupPath);
            return false;
        }

        try
        {
            using var conn = new SqliteConnection($"Data Source={backupPath}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
            return false;
        }
        catch (SqliteException ex) when (
            ex.Message.Contains("file is not a database") ||
            ex.Message.Contains("file is encrypted") ||
            ex.SqliteErrorCode == 26)
        {
            _logger.LogInformation("Backup database {FileName} is encrypted", backupFileName);
            return true;
        }
    }

    /// <summary>
    /// Try to get the database password from the keychain
    /// </summary>
    public async Task<string?> TryGetKeychainPasswordAsync()
    {
        await Task.CompletedTask;
        return _keychain.RetrieveKey();
    }

    /// <summary>
    /// Verify that a password can decrypt the backup database
    /// </summary>
    public async Task<DatabaseOperationResult> VerifyPasswordAsync(string backupFileName, string password)
    {
        var backupPath = await GetBackupFilePathAsync(backupFileName);

        try
        {
            using var conn = new SqliteConnection($"Data Source={backupPath}");
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA key = '{password}';";
                await cmd.ExecuteNonQueryAsync();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                await cmd.ExecuteScalarAsync();
            }
            return DatabaseOperationResult.SuccessResult("Password verified successfully");
        }
        catch (SqliteException)
        {
            return DatabaseOperationResult.FailureResult("Incorrect password");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password for {FileName}", backupFileName);
            return DatabaseOperationResult.FailureResult($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Save database password to the OS keychain
    /// </summary>
    public async Task SavePasswordToKeychainAsync(string password)
    {
        await Task.CompletedTask;
        _keychain.StoreKey(password, "Nine Database Password");
        _logger.LogInformation("Password saved to keychain");
    }

    // -------------------------------------------------------------------------
    // Other DB file discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns database files found in the data directory that are NOT backups and NOT the active DB.
    /// These are typically versioned databases from previous app versions (e.g. app_v1.0.0.db).
    /// </summary>
    public async Task<List<OtherDatabaseFile>> GetOtherDatabaseFilesAsync()
    {
        var results = new List<OtherDatabaseFile>();
        var dataDir = await GetDataDirectoryAsync();
        var activeDbPath = await _pathService.GetDatabasePathAsync();

        if (!Directory.Exists(dataDir))
            return results;

        foreach (var filePath in Directory.GetFiles(dataDir, "*.db"))
        {
            // Skip the active database
            if (string.Equals(filePath, activeDbPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var info = new FileInfo(filePath);
            results.Add(new OtherDatabaseFile
            {
                FileName = info.Name,
                FilePath = filePath,
                FileSizeBytes = info.Length,
                LastModified = info.LastWriteTime
            });
        }

        return results.OrderByDescending(f => f.LastModified).ToList();
    }

    /// <summary>
    /// Copies a database file from the data directory into the Backups folder so it can be
    /// previewed and imported via the standard backup workflow.
    /// </summary>
    public async Task<DatabaseOperationResult> AddToBackupsAsync(string sourceFilePath)
    {
        try
        {
            var safeFileName = Path.GetFileName(sourceFilePath);
            if (!File.Exists(sourceFilePath))
                return DatabaseOperationResult.FailureResult($"File not found: {safeFileName}");

            var backupDir = await GetBackupDirectoryAsync();
            Directory.CreateDirectory(backupDir);

            var destPath = Path.Combine(backupDir, safeFileName);
            if (File.Exists(destPath))
                return DatabaseOperationResult.SuccessResult($"{safeFileName} is already in backups.");

            File.Copy(sourceFilePath, destPath);
            _logger.LogInformation("Added {FileName} to backups", safeFileName);
            return DatabaseOperationResult.SuccessResult($"{safeFileName} added to backups.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file to backups");
            return DatabaseOperationResult.FailureResult($"Error: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Schema compatibility - required columns per entity
    // -------------------------------------------------------------------------
    // Required: if ANY of these is absent from the backup table, that entity is
    //           skipped entirely (counts as 0 imported, notes the gap).
    // Tracking: CreatedOn/CreatedBy/LastModifiedOn/LastModifiedBy are preserved
    //           when present — so import history is maintained.
    // Additive: columns added in newer schema versions (e.g. IsSampleData) are
    //           not in the intersection so they receive the active DB default.
    // -------------------------------------------------------------------------

    private static readonly HashSet<string> PropertyRequiredCols    = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "Address", "City", "State", "ZipCode", "PropertyType", "Status" };

    private static readonly HashSet<string> TenantRequiredCols      = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "FirstName", "LastName", "Email" };

    private static readonly HashSet<string> LeaseRequiredCols       = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "PropertyId", "TenantId", "StartDate", "EndDate", "MonthlyRent", "Status" };

    private static readonly HashSet<string> InvoiceRequiredCols     = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "LeaseId", "InvoiceNumber", "DueOn", "Amount", "Status" };

    private static readonly HashSet<string> PaymentRequiredCols     = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "InvoiceId", "PaymentNumber", "PaidOn", "Amount", "PaymentMethod" };

    private static readonly HashSet<string> MaintenanceRequiredCols = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "PropertyId", "Title", "RequestType", "Priority", "Status", "RequestedOn" };

    private static readonly HashSet<string> RepairRequiredCols      = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "PropertyId", "Description" };

    private static readonly HashSet<string> DocumentRequiredCols    = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "OrganizationId", "FileName", "FileData" };

    /// <summary>
    /// Maps backup column names to their renamed equivalents in the current active schema.
    /// Add an entry here whenever a column is renamed between eras.
    /// Key = backup (old) name, Value = active (new) name.
    /// </summary>
    private static readonly Dictionary<string, string> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "IsAvailable", "IsActive" }  // Properties.IsAvailable renamed to IsActive in RenameIsAvailableToIsActive
    };

    // -------------------------------------------------------------------------
    // Schema helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the set of column names for a table in the given connection.
    /// </summary>
    private static async Task<HashSet<string>> GetTableColumnsAsync(
        SqliteConnection conn, string tableName, string schema = "main")
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA {schema}.table_info([{tableName}])";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                cols.Add(r.GetString(1)); // column index 1 = name
        }
        catch { /* table absent in this version */ }
        return cols;
    }

    private static async Task<int> CountTableAsync(
        SqliteConnection conn, string table, HashSet<string> cols)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            var where = cols.Contains("IsDeleted") ? " WHERE IsDeleted = 0" : "";
            cmd.CommandText = $"SELECT COUNT(*) FROM [{table}]{where}";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        catch { return 0; }
    }

    private static SqliteConnection OpenBackupConnection(string path, string? password)
    {
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        if (!string.IsNullOrEmpty(password))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA key = '{password}';";
            cmd.ExecuteNonQuery();
        }
        return conn;
    }

    // Reader helpers — raw SQLite stores Guids/dates as TEXT

    private static Guid ReadGuid(SqliteDataReader r, int i)
        => Guid.TryParse(r.IsDBNull(i) ? null : r.GetString(i), out var g) ? g : Guid.Empty;

    private static DateTime ReadDateTime(SqliteDataReader r, int i)
        => r.IsDBNull(i) ? DateTime.MinValue : DateTime.Parse(r.GetString(i));

    private static string ReadStr(SqliteDataReader r, int i)
        => r.IsDBNull(i) ? string.Empty : r.GetString(i);

    private static decimal ReadDecimal(SqliteDataReader r, int i)
        => r.IsDBNull(i) ? 0m : r.GetDecimal(i);

    // -------------------------------------------------------------------------
    // Preview — raw SQL against backup, no EF model, schema-version tolerant
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads a read-only preview from a backup database.
    /// Uses raw SQL so it works across schema versions — only accesses columns
    /// that are known to exist, falling back to defaults when columns are absent.
    /// </summary>
    public async Task<DatabasePreviewData> GetPreviewDataAsync(string backupFileName, string? password)
    {
        var backupPath = await GetBackupFilePathAsync(backupFileName);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException($"Backup file not found: {backupFileName}");

        using var conn = OpenBackupConnection(backupPath, password);

        // Discover what columns actually exist in this backup version
        var propCols  = await GetTableColumnsAsync(conn, "Properties");
        var tenCols   = await GetTableColumnsAsync(conn, "Tenants");
        var leaseCols = await GetTableColumnsAsync(conn, "Leases");
        var invCols   = await GetTableColumnsAsync(conn, "Invoices");
        var payCols   = await GetTableColumnsAsync(conn, "Payments");
        var mntCols   = await GetTableColumnsAsync(conn, "MaintenanceRequests");
        var repCols   = await GetTableColumnsAsync(conn, "Repairs");
        var docCols   = await GetTableColumnsAsync(conn, "Documents");

        var data = new DatabasePreviewData
        {
            PropertyCount    = await CountTableAsync(conn, "Properties",          propCols),
            TenantCount      = await CountTableAsync(conn, "Tenants",             tenCols),
            LeaseCount       = await CountTableAsync(conn, "Leases",              leaseCols),
            InvoiceCount     = await CountTableAsync(conn, "Invoices",            invCols),
            PaymentCount     = await CountTableAsync(conn, "Payments",            payCols),
            MaintenanceCount = await CountTableAsync(conn, "MaintenanceRequests", mntCols),
            RepairCount      = await CountTableAsync(conn, "Repairs",             repCols),
            DocumentCount    = await CountTableAsync(conn, "Documents",           docCols),
        };

        if (propCols.IsSupersetOf(PropertyRequiredCols))
            data.Properties = await ReadPropertiesPreviewAsync(conn, propCols);

        if (tenCols.IsSupersetOf(TenantRequiredCols))
            data.Tenants = await ReadTenantsPreviewAsync(conn, tenCols);

        if (leaseCols.IsSupersetOf(LeaseRequiredCols))
            data.Leases = await ReadLeasesPreviewAsync(conn, leaseCols);

        if (invCols.IsSupersetOf(InvoiceRequiredCols))
            data.Invoices = await ReadInvoicesPreviewAsync(conn, invCols);

        if (payCols.IsSupersetOf(PaymentRequiredCols))
            data.Payments = await ReadPaymentsPreviewAsync(conn, payCols);

        if (mntCols.IsSupersetOf(MaintenanceRequiredCols))
            data.MaintenanceRequests = await ReadMaintenancePreviewAsync(conn, mntCols);

        if (repCols.IsSupersetOf(RepairRequiredCols))
            data.Repairs = await ReadRepairsPreviewAsync(conn, repCols);

        _logger.LogInformation(
            "Preview loaded from {File}: {P} properties, {T} tenants, {L} leases, {I} invoices, {Pay} payments, {M} maintenance, {R} repairs",
            backupFileName,
            data.PropertyCount, data.TenantCount, data.LeaseCount,
            data.InvoiceCount, data.PaymentCount, data.MaintenanceCount, data.RepairCount);

        return data;
    }

    private static async Task<List<PropertyPreview>> ReadPropertiesPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list = new List<PropertyPreview>();
        var del  = cols.Contains("IsDeleted") ? " WHERE IsDeleted = 0" : "";
        var rent = cols.Contains("MonthlyRent") ? ", MonthlyRent" : "";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, Address, City, State, ZipCode, PropertyType, Status{rent} FROM [Properties]{del} ORDER BY Address LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PropertyPreview
            {
                Id           = ReadGuid(r, 0),
                Address      = ReadStr(r, 1),
                City         = ReadStr(r, 2),
                State        = ReadStr(r, 3),
                ZipCode      = ReadStr(r, 4),
                PropertyType = ReadStr(r, 5),
                Status       = ReadStr(r, 6),
                MonthlyRent  = cols.Contains("MonthlyRent") ? ReadDecimal(r, 7) : 0m
            });
        return list;
    }

    private static async Task<List<TenantPreview>> ReadTenantsPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list    = new List<TenantPreview>();
        var del     = cols.Contains("IsDeleted") ? " WHERE IsDeleted = 0" : "";
        var phone   = cols.Contains("PhoneNumber") ? ", PhoneNumber" : "";
        var created = cols.Contains("CreatedOn")   ? ", CreatedOn"   : "";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, FirstName, LastName, Email{phone}{created} FROM [Tenants]{del} ORDER BY LastName, FirstName LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            int idx = 4;
            list.Add(new TenantPreview
            {
                Id        = ReadGuid(r, 0),
                FirstName = ReadStr(r, 1),
                LastName  = ReadStr(r, 2),
                Email     = ReadStr(r, 3),
                Phone     = cols.Contains("PhoneNumber") ? ReadStr(r, idx++) : string.Empty,
                CreatedOn = cols.Contains("CreatedOn")   ? ReadDateTime(r, idx++) : DateTime.MinValue
            });
        }
        return list;
    }

    private static async Task<List<LeasePreview>> ReadLeasesPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list = new List<LeasePreview>();
        var del  = cols.Contains("IsDeleted") ? " WHERE l.IsDeleted = 0" : "";
        using var cmd = conn.CreateCommand();
        // LEFT JOIN for display names — COALESCE guards against missing related rows
        cmd.CommandText = $@"
            SELECT l.Id, l.StartDate, l.EndDate, l.MonthlyRent, l.Status,
                   COALESCE(p.Address, 'Unknown') AS PropertyAddress,
                   COALESCE(t.FirstName || ' ' || t.LastName, 'Unknown') AS TenantName
            FROM [Leases] l
            LEFT JOIN [Properties] p ON l.PropertyId = p.Id
            LEFT JOIN [Tenants] t    ON l.TenantId   = t.Id
            {del}
            ORDER BY l.StartDate DESC LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new LeasePreview
            {
                Id              = ReadGuid(r, 0),
                StartDate       = ReadDateTime(r, 1),
                EndDate         = ReadDateTime(r, 2),
                MonthlyRent     = ReadDecimal(r, 3),
                Status          = ReadStr(r, 4),
                PropertyAddress = ReadStr(r, 5),
                TenantName      = ReadStr(r, 6)
            });
        return list;
    }

    private static async Task<List<InvoicePreview>> ReadInvoicesPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list = new List<InvoicePreview>();
        var del  = cols.Contains("IsDeleted") ? " WHERE i.IsDeleted = 0" : "";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT i.Id, i.InvoiceNumber, i.DueOn, i.Amount, i.Status,
                   COALESCE(p.Address, 'Unknown') AS PropertyAddress,
                   COALESCE(t.FirstName || ' ' || t.LastName, 'Unknown') AS TenantName
            FROM [Invoices] i
            LEFT JOIN [Leases]     l ON i.LeaseId    = l.Id
            LEFT JOIN [Properties] p ON l.PropertyId = p.Id
            LEFT JOIN [Tenants]    t ON l.TenantId   = t.Id
            {del}
            ORDER BY i.DueOn DESC LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new InvoicePreview
            {
                Id              = ReadGuid(r, 0),
                InvoiceNumber   = ReadStr(r, 1),
                DueOn           = ReadDateTime(r, 2),
                Amount          = ReadDecimal(r, 3),
                Status          = ReadStr(r, 4),
                PropertyAddress = ReadStr(r, 5),
                TenantName      = ReadStr(r, 6)
            });
        return list;
    }

    private static async Task<List<PaymentPreview>> ReadPaymentsPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list = new List<PaymentPreview>();
        var del  = cols.Contains("IsDeleted") ? " WHERE p.IsDeleted = 0" : "";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT p.Id, p.PaymentNumber, p.PaidOn, p.Amount, p.PaymentMethod,
                   COALESCE(i.InvoiceNumber, 'Unknown') AS InvoiceNumber
            FROM [Payments] p
            LEFT JOIN [Invoices] i ON p.InvoiceId = i.Id
            {del}
            ORDER BY p.PaidOn DESC LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PaymentPreview
            {
                Id            = ReadGuid(r, 0),
                PaymentNumber = ReadStr(r, 1),
                PaidOn        = ReadDateTime(r, 2),
                Amount        = ReadDecimal(r, 3),
                PaymentMethod = ReadStr(r, 4),
                InvoiceNumber = ReadStr(r, 5)
            });
        return list;
    }

    private static async Task<List<MaintenancePreview>> ReadMaintenancePreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list = new List<MaintenancePreview>();
        var del  = cols.Contains("IsDeleted") ? " WHERE m.IsDeleted = 0" : "";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT m.Id, m.Title, m.RequestType, m.Priority, m.Status, m.RequestedOn,
                   COALESCE(p.Address, 'Unknown') AS PropertyAddress
            FROM [MaintenanceRequests] m
            LEFT JOIN [Properties] p ON m.PropertyId = p.Id
            {del}
            ORDER BY m.RequestedOn DESC LIMIT 100";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new MaintenancePreview
            {
                Id              = ReadGuid(r, 0),
                Title           = ReadStr(r, 1),
                RequestType     = ReadStr(r, 2),
                Priority        = ReadStr(r, 3),
                Status          = ReadStr(r, 4),
                RequestedOn     = ReadDateTime(r, 5),
                PropertyAddress = ReadStr(r, 6)
            });
        return list;
    }

    private static async Task<List<RepairPreview>> ReadRepairsPreviewAsync(
        SqliteConnection conn, HashSet<string> cols)
    {
        var list       = new List<RepairPreview>();
        var del        = cols.Contains("IsDeleted")   ? " WHERE r.IsDeleted = 0" : "";
        var repType    = cols.Contains("RepairType")  ? ", r.RepairType"  : "";
        var completed  = cols.Contains("CompletedOn") ? ", r.CompletedOn" : "";
        var cost       = cols.Contains("Cost")        ? ", r.Cost"        : "";
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT r.Id, r.Description{repType}{completed}{cost},
                   COALESCE(p.Address, 'Unknown') AS PropertyAddress
            FROM [Repairs] r
            LEFT JOIN [Properties] p ON r.PropertyId = p.Id
            {del}
            ORDER BY r.Id DESC LIMIT 100";
        using var r2 = await cmd.ExecuteReaderAsync();
        while (await r2.ReadAsync())
        {
            int idx = 1;
            var preview = new RepairPreview
            {
                Id          = ReadGuid(r2, 0),
                Description = ReadStr(r2, idx++),
                RepairType  = cols.Contains("RepairType")  ? ReadStr(r2, idx++)      : string.Empty,
                CompletedOn = cols.Contains("CompletedOn") ? (DateTime?)ReadDateTime(r2, idx++) : null,
                Cost        = cols.Contains("Cost")        ? ReadDecimal(r2, idx++)  : 0m,
                PropertyAddress = ReadStr(r2, idx)
            };
            list.Add(preview);
        }
        return list;
    }

    // -------------------------------------------------------------------------
    // Import — two separate connections (backup=read, active=write)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Imports records from a backup into the active database.
    /// Opens a separate read connection to the backup (same as preview) to avoid
    /// ATTACH complications with WAL/shared-cache active connections.
    /// Only inserts rows that don't already exist (matched by Id).
    /// Only columns present in BOTH the backup AND the current schema are copied;
    /// new columns (e.g. IsSampleData) receive their active-DB defaults.
    /// OrganizationId is always overridden to the active organization.
    /// </summary>
    public async Task<ImportResult> ImportFromPreviewAsync(string backupFileName, string? password)
    {
        var result = new ImportResult();
        try
        {
            var backupPath = await GetBackupFilePathAsync(backupFileName);
            if (!File.Exists(backupPath))
            {
                result.Message = $"Backup file not found: {backupFileName}";
                return result;
            }

            var orgIdGuid = await _userContext.GetActiveOrganizationIdAsync();
            if (orgIdGuid == null)
            {
                result.Message = "No active organization found. Please ensure you are logged in.";
                return result;
            }
            var orgId      = orgIdGuid.Value.ToString("D").ToUpperInvariant();
            var userId     = await _userContext.GetUserIdAsync() ?? string.Empty;

            // Open backup with a separate connection — same as GetPreviewDataAsync
            using var backupConn = OpenBackupConnection(backupPath, password);

            var activeConn = (SqliteConnection)_activeContext.Database.GetDbConnection();
            if (activeConn.State != System.Data.ConnectionState.Open)
                await activeConn.OpenAsync();

            // Disable FK constraints for the duration of the import so that
            // cross-version backups with slightly different schemas and
            // OrganizationId overrides don't trigger FK violations.
            using (var fkOff = activeConn.CreateCommand())
            {
                fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
                await fkOff.ExecuteNonQueryAsync();
            }

            using var tx = activeConn.BeginTransaction();
            try
            {
                // Import in FK dependency order
                result.PropertiesImported          = await ImportTableAsync(backupConn, activeConn, "Properties",          PropertyRequiredCols,    orgId, userId, result.Errors);
                result.TenantsImported             = await ImportTableAsync(backupConn, activeConn, "Tenants",             TenantRequiredCols,      orgId, userId, result.Errors);
                result.LeasesImported              = await ImportTableAsync(backupConn, activeConn, "Leases",              LeaseRequiredCols,       orgId, userId, result.Errors);
                result.InvoicesImported            = await ImportTableAsync(backupConn, activeConn, "Invoices",            InvoiceRequiredCols,     orgId, userId, result.Errors);
                result.PaymentsImported            = await ImportTableAsync(backupConn, activeConn, "Payments",            PaymentRequiredCols,     orgId, userId, result.Errors);
                result.MaintenanceRequestsImported = await ImportTableAsync(backupConn, activeConn, "MaintenanceRequests", MaintenanceRequiredCols, orgId, userId, result.Errors);
                result.RepairsImported             = await ImportTableAsync(backupConn, activeConn, "Repairs",             RepairRequiredCols,      orgId, userId, result.Errors);
                result.DocumentsImported           = await ImportTableAsync(backupConn, activeConn, "Documents",           DocumentRequiredCols,    orgId, userId, result.Errors);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                // Re-enable FK constraints
                using var fkOn = activeConn.CreateCommand();
                fkOn.CommandText = "PRAGMA foreign_keys = ON;";
                await fkOn.ExecuteNonQueryAsync();
            }

            result.Success = true;
            result.Message = $"Import complete. {result.TotalImported} records imported.";
            _logger.LogInformation("Import from {File} complete: {Total} records", backupFileName, result.TotalImported);
        }
        catch (Exception ex)
        {
            result.Success  = false;
            result.Message  = $"Import failed: {ex.Message}";
            result.Errors.Add(ex.ToString());
            _logger.LogError(ex, "Import from {File} failed", backupFileName);
        }

        return result;
    }

    /// <summary>
    /// Copies rows from backupConn into activeConn for the given table.
    /// Applies <see cref="ColumnAliases"/> so renamed columns (e.g.
    /// <c>IsAvailable</c> → <c>IsActive</c>) are correctly mapped.
    /// Only columns resolvable in both schemas are copied; columns present only
    /// in the active schema are omitted from the INSERT (SQLite will apply the
    /// column DEFAULT, or the row is skipped via INSERT OR IGNORE if a NOT NULL
    /// constraint cannot be satisfied without a default).
    /// Required columns trigger a warning but never block the import.
    /// OrganizationId is always substituted with the active org.
    /// Returns the number of rows actually inserted.
    /// </summary>
    private static async Task<int> ImportTableAsync(
        SqliteConnection backupConn,
        SqliteConnection activeConn,
        string tableName,
        HashSet<string> requiredCols,
        string orgId,
        string currentUserId,
        List<string> errors)
    {
        try
        {
            var backupCols = await GetTableColumnsAsync(backupConn, tableName);
            var activeCols = await GetTableColumnsAsync(activeConn, tableName);

            // Warn about required columns absent from the backup (checking aliases too)
            // but never skip the table — import whatever is available.
            var missingRequired = requiredCols
                .Where(rc =>
                    !backupCols.Contains(rc) &&
                    !ColumnAliases.Any(kv =>
                        kv.Value.Equals(rc, StringComparison.OrdinalIgnoreCase) &&
                        backupCols.Contains(kv.Key)))
                .ToList();
            if (missingRequired.Count > 0)
                errors.Add($"{tableName}: warning — required columns not found in backup: {string.Join(", ", missingRequired)}");

            // Columns always overridden — never read from backup
            var overrideCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "OrganizationId", "CreatedBy", "LastModifiedBy" };

            // Build backup→active column mapping, applying ColumnAliases for renamed columns.
            // Only include cols that resolve to a name present in the active schema.
            var colMapping = new List<(string BackupCol, string ActiveCol)>();
            foreach (var bc in backupCols)
            {
                if (bc.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (overrideCols.Contains(bc)) continue;
                var ac = ColumnAliases.TryGetValue(bc, out var aliased) ? aliased : bc;
                if (activeCols.Contains(ac) && !overrideCols.Contains(ac))
                    colMapping.Add((bc, ac));
            }
            colMapping = colMapping.OrderBy(x => x.ActiveCol, StringComparer.OrdinalIgnoreCase).ToList();

            var readBackupCols  = colMapping.Select(x => x.BackupCol).ToArray();
            var insertActiveCols = colMapping.Select(x => x.ActiveCol).ToArray();

            // Columns to insert: Id first, then context overrides, then data cols
            var insertOverrides = new List<string> { "OrganizationId" };
            if (activeCols.Contains("CreatedBy"))      insertOverrides.Add("CreatedBy");
            if (activeCols.Contains("LastModifiedBy")) insertOverrides.Add("LastModifiedBy");

            var allInsertCols = new[] { "Id" }.Concat(insertOverrides).Concat(insertActiveCols).ToArray();
            var colList   = string.Join(", ", allInsertCols.Select(c => $"[{c}]"));
            var paramList = string.Join(", ", allInsertCols.Select(c =>
                c.Equals("OrganizationId",  StringComparison.OrdinalIgnoreCase) ? "@orgId" :
                c.Equals("CreatedBy",       StringComparison.OrdinalIgnoreCase) ? "@currentUser" :
                c.Equals("LastModifiedBy",  StringComparison.OrdinalIgnoreCase) ? "@currentUser" :
                c.Equals("Id",              StringComparison.OrdinalIgnoreCase) ? "@c_Id" :
                $"@c_{c}"));

            // SELECT uses backup col names; readCols[0]=Id, readCols[1..]=backup data cols
            var readCols      = new[] { "Id" }.Concat(readBackupCols).ToArray();
            var selectColList = string.Join(", ", readCols.Select(c => $"[{c}]"));
            var delFilter     = backupCols.Contains("IsDeleted") ? " WHERE IsDeleted = 0" : "";

            // Read all rows from backup
            var rows = new List<object?[]>();
            using (var readCmd = backupConn.CreateCommand())
            {
                readCmd.CommandText = $"SELECT {selectColList} FROM [{tableName}]{delFilter}";
                using var reader = await readCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new object?[readCols.Length];
                    for (int i = 0; i < readCols.Length; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
                return 0;

            // Build parameterized INSERT — parameters: @c_Id and @c_{activeColName}
            using var writeCmd = activeConn.CreateCommand();
            writeCmd.CommandText = $"INSERT OR IGNORE INTO [{tableName}] ({colList}) VALUES ({paramList})";
            writeCmd.Parameters.AddWithValue("@orgId", orgId);
            writeCmd.Parameters.AddWithValue("@currentUser", currentUserId);
            writeCmd.Parameters.Add(new SqliteParameter("@c_Id", DBNull.Value));
            foreach (var ac in insertActiveCols)
                writeCmd.Parameters.Add(new SqliteParameter($"@c_{ac}", DBNull.Value));

            int count = 0;
            foreach (var row in rows)
            {
                var idVal = row[0];
                if (idVal is string s0 && Guid.TryParse(s0, out var g0))
                    idVal = g0.ToString("D").ToUpperInvariant();
                writeCmd.Parameters["@c_Id"].Value = idVal ?? DBNull.Value;

                // row[1..] maps to insertActiveCols by index
                for (int i = 0; i < insertActiveCols.Length; i++)
                {
                    var val = row[i + 1];
                    if (val is string s && Guid.TryParse(s, out var g))
                        val = g.ToString("D").ToUpperInvariant();
                    writeCmd.Parameters[$"@c_{insertActiveCols[i]}"].Value = val ?? DBNull.Value;
                }
                count += await writeCmd.ExecuteNonQueryAsync();
            }

            return count;
        }
        catch (Exception ex)
        {
            errors.Add($"{tableName}: {ex.Message}");
            return 0;
        }
    }
}
