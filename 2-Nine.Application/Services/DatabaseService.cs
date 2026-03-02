using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        _logger.LogInformation("Checking for pending migrations...");
        
        // Check and apply identity migrations first
        var identityPending = await _identityContext.Database.GetPendingMigrationsAsync();
        if (identityPending.Any())
        {
            _logger.LogInformation($"Applying {identityPending.Count()} identity migrations...");
            await _identityContext.Database.MigrateAsync();
            _logger.LogInformation("Identity migrations applied successfully.");
        }
        else
        {
            _logger.LogInformation("No pending identity migrations.");
        }
        
        // Then check and apply business migrations
        var businessPending = await _businessContext.Database.GetPendingMigrationsAsync();
        if (businessPending.Any())
        {
            _logger.LogInformation($"Applying {businessPending.Count()} business migrations...");
            await _businessContext.Database.MigrateAsync();
            _logger.LogInformation("Business migrations applied successfully.");
        }
        else
        {
            _logger.LogInformation("No pending business migrations.");
        }
        
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

    /// <summary>
    /// Gets the database settings (creates default if not exists)
    /// </summary>
    public async Task<Nine.Core.Entities.DatabaseSettings> GetDatabaseSettingsAsync()
    {
        var settings = await _businessContext.DatabaseSettings.FirstOrDefaultAsync();

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

