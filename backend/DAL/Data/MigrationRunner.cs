using Microsoft.Extensions.Logging;
using Npgsql;
using System.Collections.Generic;

namespace DAL.Data
{
    /// <summary>
    /// Runs numbered SQL migration files from the database/migrations folder on startup.
    /// Tracks applied migrations in a schema_migrations table so each file only runs once.
    /// Files must be named NNN_description.sql (e.g. 001_fix_something.sql).
    /// </summary>
    public static class MigrationRunner
    {
        private static readonly HashSet<string> IdempotentConflictSqlStates = new()
        {
            PostgresErrorCodes.DuplicateColumn,      // 42701
            PostgresErrorCodes.DuplicateTable,       // 42P07
            PostgresErrorCodes.DuplicateObject,      // 42710
            PostgresErrorCodes.DuplicateFunction,    // 42723
            PostgresErrorCodes.DuplicateDatabase,    // 42P04
            PostgresErrorCodes.DuplicateAlias,       // 42712
            PostgresErrorCodes.DuplicateFile         // 42710 alias in some providers
        };

        public static void Run(string connectionString, string migrationsFolder, ILogger logger)
        {
            if (!Directory.Exists(migrationsFolder))
            {
                logger.LogWarning("Migrations folder not found: {Folder}", migrationsFolder);
                return;
            }

            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            // Ensure the tracking table exists
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        filename TEXT PRIMARY KEY,
                        applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );
                    """;
                cmd.ExecuteNonQuery();
            }

            // Get already-applied migrations
            var applied = new HashSet<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT filename FROM schema_migrations;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    applied.Add(reader.GetString(0));
            }

            // Run pending migrations in order
            var files = Directory.GetFiles(migrationsFolder, "*.sql")
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                if (applied.Contains(filename))
                {
                    logger.LogDebug("Migration already applied: {File}", filename);
                    continue;
                }

                logger.LogInformation("Applying migration: {File}", filename);
                var sql = File.ReadAllText(file);

                using var tx = conn.BeginTransaction();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO schema_migrations (filename) VALUES (@f);";
                        cmd.Parameters.AddWithValue("f", filename);
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    logger.LogInformation("Migration applied successfully: {File}", filename);
                }
                catch (PostgresException pgEx) when (IdempotentConflictSqlStates.Contains(pgEx.SqlState))
                {
                    tx.Rollback();
                    logger.LogWarning(
                        "Migration hit an idempotent conflict and will be marked as applied: {File}. SQLSTATE={SqlState} Message={Message}",
                        filename,
                        pgEx.SqlState,
                        pgEx.MessageText);

                    MarkMigrationAsApplied(conn, filename);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    logger.LogError(ex, "Migration failed: {File}. Rolling back.", filename);
                    throw;
                }
            }
        }

        private static void MarkMigrationAsApplied(NpgsqlConnection conn, string filename)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO schema_migrations (filename) VALUES (@f) ON CONFLICT (filename) DO NOTHING;";
            cmd.Parameters.AddWithValue("f", filename);
            cmd.ExecuteNonQuery();
        }
    }
}

