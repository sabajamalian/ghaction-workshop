using Npgsql;
using Xunit;

namespace WorkshopCore.IntegrationTests;

// These tests need a PostgreSQL server. In Lab 17, a workflow starts one as a service container and passes its
// connection string in the WORKSHOP_POSTGRES environment variable. Without that variable, every test is skipped.
public class CalculationStoreTests
{
    private const string ConnectionVariable = "WORKSHOP_POSTGRES";

    [Fact]
    public async Task Postgres_can_be_reached()
    {
        await using NpgsqlConnection connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT version()", connection);

        object? version = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("PostgreSQL", Assert.IsType<string>(version));
    }

    [Fact]
    public async Task Postgres_and_the_calculator_agree()
    {
        await using NpgsqlConnection connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT @a + @b, @a - @b", connection);
        command.Parameters.AddWithValue("a", 12);
        command.Parameters.AddWithValue("b", 5);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Calculator.Add(12, 5), reader.GetInt32(0));
        Assert.Equal(Calculator.Subtract(12, 5), reader.GetInt32(1));
    }

    [Fact]
    public async Task Saved_results_can_be_read_back()
    {
        await using NpgsqlConnection connection = await OpenAsync();

        // A temporary table belongs to this connection only and disappears when it closes.
        await ExecuteAsync(connection, "CREATE TEMPORARY TABLE results (id serial PRIMARY KEY, operation text NOT NULL, value integer NOT NULL)");
        await SaveAsync(connection, "add", Calculator.Add(2, 3));
        await SaveAsync(connection, "subtract", Calculator.Subtract(10, 4));

        await using var query = new NpgsqlCommand("SELECT operation, value FROM results ORDER BY id", connection);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var saved = new List<(string Operation, int Value)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            saved.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        Assert.Equal([("add", 5), ("subtract", 6)], saved);
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"Set {ConnectionVariable} to a PostgreSQL connection string to run this test.");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task SaveAsync(NpgsqlConnection connection, string operation, int value)
    {
        await using var command = new NpgsqlCommand("INSERT INTO results (operation, value) VALUES (@operation, @value)", connection);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
