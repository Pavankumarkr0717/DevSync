using System.Net.Sockets;
using Npgsql;

namespace DevSync.Infrastructure.Persistence;

/// <summary>Classifies exceptions so the API can report an unreachable database as 503.</summary>
public static class DatabaseErrors
{
    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgres:
                    // The server sent an error. Only connection loss, shutdown and resource exhaustion
                    // mean "unavailable"; constraint violations and similar are real errors.
                    return IsUnavailableSqlState(postgres.SqlState);
                case NpgsqlException { IsTransient: true }:
                case SocketException:
                    return true;
            }
        }

        return false;
    }

    // 08xxx connection exception, 57P01-57P03 admin/crash shutdown or cannot connect now,
    // 53xxx insufficient resources (e.g. too many connections).
    private static bool IsUnavailableSqlState(string sqlState) =>
        sqlState.StartsWith("08", StringComparison.Ordinal)
        || sqlState is PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown or PostgresErrorCodes.CannotConnectNow
        || sqlState.StartsWith("53", StringComparison.Ordinal);
}
