using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Shora.Tests.Common;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"Shora_Test_{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();

        var builder = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }
}
