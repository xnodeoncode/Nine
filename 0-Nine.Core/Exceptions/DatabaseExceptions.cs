namespace Nine.Core.Exceptions;

/// <summary>
/// Groups database-specific exceptions thrown by the Nine database layer.
/// </summary>
public static class DatabaseExceptions
{
    /// <summary>
    /// Thrown when the database schema is more than two major versions behind the
    /// current release and cannot be bridged automatically.  The database has been
    /// copied to <see cref="SchemaNotSupportedException.BackupPath"/> before this
    /// exception is thrown; the user must import their data via the application's
    /// import workflow instead.
    /// </summary>
    public class SchemaNotSupportedException : Exception
    {
        /// <summary>Full path of the backup copy made before this exception was thrown.</summary>
        public string BackupPath { get; }

        public SchemaNotSupportedException(string backupPath)
            : base(
                "The database has an incompatible schema version and cannot be upgraded " +
                $"automatically. A backup has been saved to: {backupPath}")
        {
            BackupPath = backupPath;
        }
    }

    /// <summary>
    /// Thrown when the database schema structure is invalid, unrecognised, or
    /// internally inconsistent in a way that prevents normal operation.
    /// </summary>
    public class SchemaInvalidException : Exception
    {
        public SchemaInvalidException(string message) : base(message) { }

        public SchemaInvalidException(string message, Exception inner)
            : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when an era bridge migration step fails.  Used inside
    /// <c>ApplyFirstAncestorBridgeAsync</c> and <c>ApplySecondAncestorBridgeAsync</c>
    /// to surface a descriptive failure without leaking raw SQL exception details to
    /// the UI layer.
    /// </summary>
    public class MigrationException : Exception
    {
        public MigrationException(string message) : base(message) { }

        public MigrationException(string message, Exception inner)
            : base(message, inner) { }
    }
}
