using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Nine.Infrastructure.Data;

/// <summary>
/// EF Core connection interceptor that sets the SQLCipher encryption key on every connection open.
///
/// Performance note: accepts either a plaintext passphrase or a pre-derived raw key in SQLCipher's
/// "x'hexbytes'" format. Raw keys skip the per-connection PBKDF2 derivation entirely, which
/// eliminates 20–50 ms of CPU work per connection. Use SqlCipherKeyDerivation.DeriveRawKey()
/// at startup to produce the raw key from the user's passphrase, then pass it here.
/// </summary>
public class SqlCipherConnectionInterceptor : DbConnectionInterceptor
{
    private readonly string? _encryptionKey;

    /// <param name="encryptionKey">
    /// Either a plaintext passphrase (SQLCipher runs PBKDF2 internally, slow)
    /// or a pre-derived raw key in x'hexbytes' format (no PBKDF2, fast).
    /// </param>
    public SqlCipherConnectionInterceptor(string? encryptionKey)
    {
        _encryptionKey = encryptionKey;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!string.IsNullOrEmpty(_encryptionKey))
        {
            using var cmd = connection.CreateCommand();

            if (_encryptionKey.StartsWith("x'", StringComparison.OrdinalIgnoreCase))
            {
                // Pre-derived raw key — SQLCipher loads it directly, no PBKDF2 (~0 ms)
                cmd.CommandText = $"PRAGMA key = \"{_encryptionKey}\";";
                cmd.ExecuteNonQuery();

                // Raw key still needs cipher params to match how the DB was encrypted
                cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                cmd.ExecuteNonQuery();
            }
            else
            {
                // Passphrase fallback — SQLCipher runs PBKDF2(256000) internally (~20–50 ms)
                cmd.CommandText = $"PRAGMA key = '{_encryptionKey}';"; 
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA kdf_iter = 256000;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                cmd.ExecuteNonQuery();
            }
        }

        // Always set busy_timeout and WAL mode regardless of encryption.
        // busy_timeout: converts indefinite lock-wait hangs into a 5-second timeout error.
        // journal_mode = WAL: allows concurrent readers during writes; must be a persistent DB
        //   setting so it is set on every connection open (idempotent — SQLite ignores it if
        //   already WAL).  Must run AFTER PRAGMA key for encrypted databases.
        using (var walCmd = connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA busy_timeout = 5000;";
            walCmd.ExecuteNonQuery();
            walCmd.CommandText = "PRAGMA journal_mode = WAL;";
            walCmd.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_encryptionKey))
        {
            using var cmd = connection.CreateCommand();

            if (_encryptionKey.StartsWith("x'", StringComparison.OrdinalIgnoreCase))
            {
                // Pre-derived raw key — SQLCipher loads it directly, no PBKDF2 (~0 ms)
                cmd.CommandText = $"PRAGMA key = \"{_encryptionKey}\";";
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Raw key still needs cipher params to match how the DB was encrypted
                cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                // Passphrase fallback — SQLCipher runs PBKDF2(256000) internally (~20–50 ms)
                cmd.CommandText = $"PRAGMA key = '{_encryptionKey}';";
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.CommandText = "PRAGMA kdf_iter = 256000;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // Always set busy_timeout and WAL mode regardless of encryption (see sync overload above).
        using (var walCmd = connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA busy_timeout = 5000;";
            await walCmd.ExecuteNonQueryAsync(cancellationToken);
            walCmd.CommandText = "PRAGMA journal_mode = WAL;";
            await walCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
