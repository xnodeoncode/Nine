using Microsoft.EntityFrameworkCore;
using Nine.Application.Services;
using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;

namespace Nine.Shared.Services
{
    /// <summary>
    /// Builds the detailed database health report shown on the Database Settings page.
    /// </summary>
    public class DatabaseHealthService
    {
        public const long LargeDatabaseBytes = 500L * 1024 * 1024;
        public const int StaleBackupDays = 7;

        private readonly ILogger<DatabaseHealthService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IUserContextService _userContext;
        private readonly SchemaValidationService _schemaService;
        private readonly DatabaseBackupService _backupService;

        public DatabaseHealthService(
            ILogger<DatabaseHealthService> logger,
            ApplicationDbContext dbContext,
            IUserContextService userContext,
            SchemaValidationService schemaService,
            DatabaseBackupService backupService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userContext = userContext;
            _schemaService = schemaService;
            _backupService = backupService;
        }

        /// <summary>
        /// Runs the connection, integrity, schema, file, record count and backup checks.
        /// </summary>
        public async Task<DatabaseHealthReport> GetHealthReportAsync()
        {
            var report = new DatabaseHealthReport();

            try
            {
                var dbPath = await _backupService.GetDatabasePathAsync();
                report.DatabasePath = dbPath;
                report.DatabaseFileName = Path.GetFileName(dbPath);
                if (File.Exists(dbPath))
                {
                    report.FileSizeBytes = new FileInfo(dbPath).Length;
                }

                await CheckConnectionAndIntegrityAsync(report);

                if (report.IsConnected)
                {
                    var (isValid, message, version) = await _schemaService.ValidateSchemaVersionAsync();
                    report.SchemaVersion = version;
                    report.SchemaMatches = isValid;
                    report.SchemaMessage = message;

                    var orgId = await _userContext.GetActiveOrganizationIdAsync();
                    if (orgId.HasValue && orgId.Value != Guid.Empty)
                    {
                        report.RecordCounts = await CountActiveRecordsAsync(_dbContext, orgId.Value);
                    }
                }

                var backups = await _backupService.GetAvailableBackupsAsync();
                report.BackupCount = backups.Count;
                report.LastBackupDate = backups.Count > 0 ? backups.Max(b => b.CreatedDate) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health report failed");
                report.IsConnected = false;
                report.ConnectionMessage = ex.Message;
            }
            finally
            {
                try
                {
                    await _dbContext.Database.CloseConnectionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing database connection after health check");
                }
            }

            ApplyIndicators(report, DateTime.Now);
            return report;
        }

        private async Task CheckConnectionAndIntegrityAsync(DatabaseHealthReport report)
        {
            try
            {
                await _dbContext.Database.OpenConnectionAsync();
                report.IsConnected = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check could not connect");
                report.IsConnected = false;
                report.ConnectionMessage = ex.Message;
                return;
            }

            try
            {
                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";
                var result = (await command.ExecuteScalarAsync())?.ToString() ?? "unknown";

                report.IntegrityPassed = result.Equals("ok", StringComparison.OrdinalIgnoreCase);
                report.IntegrityMessage = result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database integrity check failed to run");
                report.IntegrityPassed = false;
                report.IntegrityMessage = ex.Message;
            }
        }

        /// <summary>
        /// Counts records for one organization, excluding soft-deleted and archived rows.
        /// The entity-specific IsActive flag is intentionally not used.
        /// </summary>
        public static async Task<DatabaseRecordCounts> CountActiveRecordsAsync(ApplicationDbContext db, Guid organizationId)
        {
            return new DatabaseRecordCounts
            {
                Properties = await db.Properties.CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && !e.IsArchived),
                Tenants = await db.Tenants.CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && !e.IsArchived),
                Leases = await db.Leases.CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && !e.IsArchived),
                Invoices = await db.Invoices.CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && !e.IsArchived),
                Payments = await db.Payments.CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && !e.IsArchived)
            };
        }

        /// <summary>
        /// Derives the warning (yellow) and error (red) indicators from the collected report data.
        /// </summary>
        public static void ApplyIndicators(DatabaseHealthReport report, DateTime now)
        {
            report.Errors.Clear();
            report.Warnings.Clear();

            if (!report.IsConnected)
            {
                report.Errors.Add($"Connection failed: {report.ConnectionMessage}");
            }
            else
            {
                if (!report.IntegrityPassed)
                {
                    report.Errors.Add($"Integrity check failed: {report.IntegrityMessage}");
                }

                if (!report.SchemaMatches)
                {
                    report.Errors.Add(string.IsNullOrEmpty(report.SchemaMessage)
                        ? "Schema version could not be verified"
                        : report.SchemaMessage);
                }
            }

            if (report.FileSizeBytes > LargeDatabaseBytes)
            {
                report.Warnings.Add("Database file is over 500 MB. Consider cleaning up old data.");
            }

            if (report.LastBackupDate == null)
            {
                report.Warnings.Add("No backups found. Create a backup.");
            }
            else if ((now - report.LastBackupDate.Value).TotalDays > StaleBackupDays)
            {
                var days = (int)(now - report.LastBackupDate.Value).TotalDays;
                report.Warnings.Add($"No recent backup. The last backup was {days} days ago.");
            }
        }
    }

    public class DatabaseHealthReport
    {
        public bool IsConnected { get; set; }
        public string ConnectionMessage { get; set; } = string.Empty;
        public bool IntegrityPassed { get; set; }
        public string IntegrityMessage { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public string DatabaseFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? SchemaVersion { get; set; }
        public bool SchemaMatches { get; set; }
        public string SchemaMessage { get; set; } = string.Empty;
        public DatabaseRecordCounts? RecordCounts { get; set; }
        public DateTime? LastBackupDate { get; set; }
        public int BackupCount { get; set; }
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public bool IsHealthy => Errors.Count == 0;
    }

    public class DatabaseRecordCounts
    {
        public int Properties { get; set; }
        public int Tenants { get; set; }
        public int Leases { get; set; }
        public int Invoices { get; set; }
        public int Payments { get; set; }
    }
}
