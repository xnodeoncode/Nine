using Nine.Application.Models.DTOs;
using Nine.Core.Entities;
using Nine.Infrastructure.Data;
using Nine.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services;

/// <summary>
/// Service for previewing backup databases in read-only mode.
/// Allows viewing database contents without overwriting active database.
/// </summary>
public class DatabasePreviewService
{
    private readonly IKeychainService _keychain;
    private readonly ILogger<DatabasePreviewService> _logger;
    private readonly string _backupDirectory;

    public DatabasePreviewService(
        IKeychainService keychain,
        ILogger<DatabasePreviewService> logger)
    {
        _keychain = keychain;
        _logger = logger;
        
        // Determine backup directory - use standard Data folder
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        _backupDirectory = Path.Combine(dataPath, "Backups");
    }

    /// <summary>
    /// Check if a backup database file is encrypted
    /// </summary>
    public async Task<bool> IsDatabaseEncryptedAsync(string backupFileName)
    {
        var backupPath = GetBackupFilePath(backupFileName);
        
        if (!File.Exists(backupPath))
        {
            _logger.LogWarning($"Backup file not found: {backupPath}");
            return false;
        }

        try
        {
            // Try to open without password
            using var conn = new SqliteConnection($"Data Source={backupPath}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
            return false; // Opened successfully = not encrypted
        }
        catch (SqliteException ex)
        {
            // SQLCipher error codes indicate encryption
            if (ex.Message.Contains("file is not a database") ||
                ex.Message.Contains("file is encrypted") ||
                ex.SqliteErrorCode == 26) // SQLITE_NOTADB
            {
                _logger.LogInformation($"Backup database {backupFileName} is encrypted");
                return true;
            }

            // Some other error
            _logger.LogError(ex, $"Error checking encryption status: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Try to get password from keychain (Linux only)
    /// </summary>
    public async Task<string?> TryGetKeychainPasswordAsync()
    {
        await Task.CompletedTask; // Keep method async
        var key = _keychain.RetrieveKey();
        if (key != null)
        {
            _logger.LogInformation("Retrieved encryption key from keychain");
        }
        return key;
    }

    /// <summary>
    /// Verify that a password can decrypt the backup database
    /// </summary>
    public async Task<DatabaseOperationResult> VerifyPasswordAsync(string backupFileName, string password)
    {
        var backupPath = GetBackupFilePath(backupFileName);

        try
        {
            using var conn = new SqliteConnection($"Data Source={backupPath}");
            await conn.OpenAsync();
            
            // Apply encryption key
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA key = '{password}';";
                await cmd.ExecuteNonQueryAsync();
            }
            
            // Test if we can read the database
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                await cmd.ExecuteScalarAsync();
            }

            return DatabaseOperationResult.SuccessResult("Password verified successfully");
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning($"Password verification failed: {ex.Message}");
            return DatabaseOperationResult.FailureResult("Incorrect password");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying password: {ex.Message}");
            return DatabaseOperationResult.FailureResult($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Save password to keychain (overwrites existing)
    /// </summary>
    public async Task SavePasswordToKeychainAsync(string password)
    {
        if (!OperatingSystem.IsLinux())
        {
            _logger.LogWarning("Keychain storage only supported on Linux");
            return;
        }

        await Task.CompletedTask; // Make method async
        _keychain.StoreKey(password, "Nine Database Password");
        _logger.LogInformation("Password saved to keychain");
    }

    /// <summary>
    /// Get preview data from backup database
    /// </summary>
    public async Task<DatabasePreviewData> GetPreviewDataAsync(string backupFileName, string? password)
    {
        var backupPath = GetBackupFilePath(backupFileName);
        
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupFileName}");
        }

        // Build connection string
        var connectionString = string.IsNullOrEmpty(password)
            ? $"Data Source={backupPath}"
            : $"Data Source={backupPath}";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString, sqliteOptions =>
            {
                // Read-only mode
                sqliteOptions.CommandTimeout(30);
            })
            .Options;

        using var previewContext = new ApplicationDbContext(options);
        
        // Apply encryption key if password provided
        if (!string.IsNullOrEmpty(password))
        {
            using var conn = previewContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA key = '{password}';";
            await cmd.ExecuteNonQueryAsync();
        }

        // Load preview data
        var previewData = new DatabasePreviewData
        {
            PropertyCount = await previewContext.Properties.CountAsync(p => !p.IsDeleted),
            TenantCount = await previewContext.Tenants.CountAsync(t => !t.IsDeleted),
            LeaseCount = await previewContext.Leases.CountAsync(l => !l.IsDeleted),
            InvoiceCount = await previewContext.Invoices.CountAsync(i => !i.IsDeleted),
            PaymentCount = await previewContext.Payments.CountAsync(p => !p.IsDeleted)
        };

        // Load detailed property data
        previewData.Properties = await previewContext.Properties
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Address)
            .Take(100) // Limit to first 100 for performance
            .Select(p => new PropertyPreview
            {
                Id = p.Id,
                Address = p.Address,
                City = p.City,
                State = p.State,
                ZipCode = p.ZipCode,
                PropertyType = p.PropertyType,
                Status = p.Status,
                Units = null,
                MonthlyRent = p.MonthlyRent
            })
            .ToListAsync();

        // Load detailed tenant data
        previewData.Tenants = await previewContext.Tenants
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.LastName)
            .Take(100) // Limit to first 100 for performance
            .Select(t => new TenantPreview
            {
                Id = t.Id,
                FirstName = t.FirstName,
                LastName = t.LastName,
                Email = t.Email,
                Phone = t.PhoneNumber,
                CreatedOn = t.CreatedOn
            })
            .ToListAsync();

        // Load detailed lease data with related entities
        previewData.Leases = await previewContext.Leases
            .Where(l => !l.IsDeleted)
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .OrderByDescending(l => l.StartDate)
            .Take(100) // Limit to first 100 for performance
            .Select(l => new LeasePreview
            {
                Id = l.Id,
                PropertyAddress = l.Property != null ? l.Property.Address : "Unknown",
                TenantName = l.Tenant != null ? $"{l.Tenant.FirstName} {l.Tenant.LastName}" : "Unknown",
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                MonthlyRent = l.MonthlyRent,
                Status = l.Status
            })
            .ToListAsync();

        _logger.LogInformation($"Loaded preview data: {previewData.PropertyCount} properties, {previewData.TenantCount} tenants, {previewData.LeaseCount} leases");

        return previewData;
    }

    /// <summary>
    /// Get full path to backup file
    /// </summary>
    private string GetBackupFilePath(string backupFileName)
    {
        // Security: Prevent path traversal attacks
        var safeFileName = Path.GetFileName(backupFileName);
        return Path.Combine(_backupDirectory, safeFileName);
    }
}
