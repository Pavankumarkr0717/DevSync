using System.Net.Http.Json;
using System.Text;
using DevSync.IntegrationTests.Infrastructure;
using Npgsql;

namespace DevSync.IntegrationTests.Security;

/// <summary>
/// After a full scripted session, no password, refresh token or access token appears in the
/// database, the logs or any response body (FR-004, FR-014, NFR-001, SC-005).
/// </summary>
public sealed class SecretExposureTests(PostgresFixture postgres)
{
    private const string Password = "Zebra-Distinctive-Password-7731";

    [Fact]
    public async Task Secrets_never_appear_in_the_database_logs_or_responses()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: logs);
        var refreshTokens = new List<string>();
        var accessTokens = new List<string>();

        var ana = AuthClient.Create(factory);
        await ana.RegisterAsync("ana@example.com", Password, "Ana");
        Collect(ana);
        await ana.LoginAsync("ana@example.com", "wrong-" + Password); // failed attempt
        await ana.RefreshAsync();
        Collect(ana);

        var anaApi = ana.BearerClient(factory);
        var workspace = await anaApi.CreateWorkspaceAsync("Secrets test");
        var workspaceId = workspace.GetProperty("id").GetGuid();
        var invitation = await (await anaApi.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invitations",
            new { email = "ben@example.com", role = "editor" })).ReadJsonAsync();

        var ben = AuthClient.Create(factory);
        await ben.RegisterAsync("ben@example.com", Password + "-ben", "Ben");
        Collect(ben);
        await ben.BearerClient(factory).PostAsync($"/api/invitations/{invitation.GetProperty("id").GetGuid()}/accept", null);
        await ben.LogoutAsync();
        await ana.LogoutAllAsync();

        var secrets = new List<string> { Password, Password + "-ben" };
        secrets.AddRange(refreshTokens);
        secrets.AddRange(accessTokens);

        var database = await DumpDatabaseAsync(factory.ConnectionString);
        var responses = string.Join("\n", ana.ResponseBodies.Concat(ben.ResponseBodies));
        var logText = string.Join("\n", logs.Messages);

        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, database, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, logText, StringComparison.Ordinal);
        }

        // Responses legitimately carry the access token that was just issued, never passwords or refresh tokens.
        foreach (var secret in secrets.Except(accessTokens))
        {
            Assert.DoesNotContain(secret, responses, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("password_hash", responses, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", responses, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("devsync_rt=", logText, StringComparison.Ordinal);

        void Collect(AuthClient client)
        {
            if (client.RefreshToken is { } r)
            {
                refreshTokens.Add(r);
            }

            if (client.AccessToken is { } a)
            {
                accessTokens.Add(a);
            }
        }
    }

    /// <summary>Every text, varchar and bytea value in every public table, as one string (bytea as UTF-8 and hex).</summary>
    private static async Task<string> DumpDatabaseAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var columns = new List<(string Table, string Column, string Type)>();
        await using (var command = new NpgsqlCommand("""
            SELECT table_name, column_name, data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type IN ('text', 'character varying', 'bytea')
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        var dump = new StringBuilder();
        foreach (var (table, column, type) in columns)
        {
            // Identifiers come from information_schema, quoted; no user input is involved.
            var sql = $"SELECT \"{column}\" FROM \"{table}\" WHERE \"{column}\" IS NOT NULL";
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (type == "bytea")
                {
                    var bytes = (byte[])reader.GetValue(0);
                    dump.Append(Encoding.UTF8.GetString(bytes)).Append('\n').Append(Convert.ToHexString(bytes)).Append('\n');
                }
                else
                {
                    dump.Append(reader.GetString(0)).Append('\n');
                }
            }
        }

        return dump.ToString();
    }
}
