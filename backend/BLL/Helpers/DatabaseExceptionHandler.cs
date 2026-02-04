using Npgsql;

namespace BLL.Helpers
{
    /// <summary>
    /// Helper class for handling database exceptions and providing user-friendly error messages
    /// </summary>
    public static class DatabaseExceptionHandler
    {
        /// <summary>
        /// Handles database unique constraint violations and throws user-friendly exceptions
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <exception cref="Exception">Throws user-friendly exception for known constraint violations</exception>
        public static void HandleUniqueConstraintViolation(Exception ex)
        {
            // Try to get PostgreSQL-specific exception first (more reliable)
            var pgException = ex as PostgresException ?? ex.InnerException as PostgresException;

            if (pgException != null)
            {
                // PostgreSQL error code 23505 = unique_violation
                if (pgException.SqlState == "23505")
                {
                    HandlePostgresUniqueViolation(pgException);
                }
                return;
            }

            // Fallback to message parsing for other database providers or wrapped exceptions
            if (ex.InnerException?.Message.Contains("duplicate key") is true ||
                ex.Message.Contains("duplicate key"))
            {
                HandleDuplicateKeyByMessage(ex);
            }
        }

        private static void HandlePostgresUniqueViolation(PostgresException pgException)
        {
            // PostgreSQL includes constraint name in the exception
            var constraintName = pgException.ConstraintName?.ToLower() ?? string.Empty;
            var detailMessage = pgException.Detail?.ToLower() ?? string.Empty;
            var message = pgException.Message?.ToLower() ?? string.Empty;

            if (constraintName.Contains("email") || detailMessage.Contains("email") || message.Contains("email"))
            {
                throw new Exception("Email address already exists in the system");
            }
            else if (constraintName.Contains("phone") || detailMessage.Contains("phone") || message.Contains("phone"))
            {
                throw new Exception("Phone number already exists in the system");
            }
            else if (constraintName.Contains("student_code") || detailMessage.Contains("student_code") || message.Contains("student_code"))
            {
                throw new Exception("Student code already exists in the system");
            }
            else if (constraintName.Contains("github") || detailMessage.Contains("github") || message.Contains("github"))
            {
                throw new Exception("GitHub username already exists in the system");
            }
            else if (constraintName.Contains("jira") || detailMessage.Contains("jira") || message.Contains("jira"))
            {
                throw new Exception("Jira account ID already exists in the system");
            }
            else
            {
                // Generic unique constraint violation
                throw new Exception("A unique constraint violation occurred. Please check your input.");
            }
        }

        private static void HandleDuplicateKeyByMessage(Exception ex)
        {
            var errorMessage = (ex.InnerException?.Message ?? ex.Message).ToLower();

            if (errorMessage.Contains("email"))
            {
                throw new Exception("Email address already exists in the system");
            }
            else if (errorMessage.Contains("phone"))
            {
                throw new Exception("Phone number already exists in the system");
            }
            else if (errorMessage.Contains("student_code"))
            {
                throw new Exception("Student code already exists in the system");
            }
            else if (errorMessage.Contains("github"))
            {
                throw new Exception("GitHub username already exists in the system");
            }
            else if (errorMessage.Contains("jira"))
            {
                throw new Exception("Jira account ID already exists in the system");
            }
            else
            {
                // Generic duplicate key error
                throw new Exception("A duplicate record was detected. Please check your input.");
            }
        }

        /// <summary>
        /// Checks if an exception is a database unique constraint violation
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if it's a unique constraint violation</returns>
        public static bool IsUniqueConstraintViolation(Exception ex)
        {
            // Check for PostgreSQL-specific exception
            var pgException = ex as PostgresException ?? ex.InnerException as PostgresException;
            if (pgException != null && pgException.SqlState == "23505")
            {
                return true;
            }

            // Fallback to message checking
            return ex.InnerException?.Message.Contains("duplicate key") is true ||
                   ex.Message.Contains("duplicate key");
        }
    }
}
