using Visor.PostgreSql; 
using Xunit;
using Npgsql;
using Visor.Core;
using Visor.IntegrationTests.Postgres.Stabs;

namespace Visor.IntegrationTests.Postgres;

public class PostgreSqlRealDbTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=VisorTestDb;Username=postgres;Password=VisorStrongPass123!";

    [Fact]
    public async Task Pg_FullFlow_Test()
    {
        // Arrange: 1. Configure the NpgsqlDataSource using the generated bootstrapper.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
            
        // Use the generated extension method to map composite types.
        dataSourceBuilder.UseVisor(); 
            
        await using var dataSource = dataSourceBuilder.Build();

        // Arrange: 2. Create the connection factory.
        IVisorConnectionFactory factory = new PostgreSqlConnectionFactory(dataSource);

        // Arrange: 3. Create the repository instance.
        var repository = new PgUserRepository(factory);

        // --- TEST ---

        // Arrange: A. Truncate the table for a clean test run.
        await using (var connection = dataSource.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("TRUNCATE TABLE users", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Act: B. Insert data using a composite type array.
        var users = new List<PgUserCompositeType>
        {
            new() { Name = "PgUser 1" },
            new() { Name = "PgUser 2" }
        };

        await repository.ImportUsers(users);

        // Assert: C. Verify the insertion.
        var count = await repository.GetCount();
        Assert.Equal(2, count);
            
        // Assert: D. Verify list mapping on read.
        var all = await repository.GetAll();
        Assert.Contains(all, user => user.Name == "PgUser 1");
    }
}
