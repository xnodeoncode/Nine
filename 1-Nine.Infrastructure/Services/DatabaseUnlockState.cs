namespace Nine.Infrastructure.Services;

/// <summary>
/// Singleton service tracking database encryption unlock state during app lifecycle
/// </summary>
public class DatabaseUnlockState
{
    public bool NeedsUnlock { get; set; }
    public string? DatabasePath { get; set; }
    public string? ConnectionString { get; set; }

    /// <summary>
    /// True when an encrypted sibling DB was found during the version-scan but the keychain
    /// had no key at startup. After the user provides their password it is stored in the
    /// keychain, but a full process restart is required for startup to re-run the version-scan
    /// copy and run pending migrations. A forceLoad (Blazor circuit reconnect) is not enough.
    /// </summary>
    public bool RequiresRestartAfterUnlock { get; set; }

    /// <summary>
    /// The file path of the encrypted sibling DB that was found during version-scan.
    /// Set when <see cref="RequiresRestartAfterUnlock"/> is true. The unlock page must verify
    /// the user's password against this path rather than <see cref="ConnectionString"/> because
    /// opening the non-existent target path with a password would cause SQLite to silently
    /// create a fresh empty database there, poisoning the version-scan copy on the next launch.
    /// </summary>
    public string? EncryptedSiblingPath { get; set; }

    /// <summary>
    /// True when the database era is outside the supported upgrade window (more than two
    /// generations behind). The database has already been backed up to
    /// <see cref="UnsupportedSchemaBackupPath"/> before this flag is set. The user must
    /// start fresh or use the import workflow; they cannot simply unlock and continue.
    /// </summary>
    public bool IsUnsupportedSchema { get; set; }

    /// <summary>
    /// Full path of the backup copy created before the unsupported-schema condition was
    /// detected. Displayed in the UI so the user knows their data is safe.
    /// </summary>
    public string? UnsupportedSchemaBackupPath { get; set; }

    // Event to notify when unlock succeeds
    public event Action? OnUnlockSuccess;
    
    public void NotifyUnlockSuccess() => OnUnlockSuccess?.Invoke();
}
