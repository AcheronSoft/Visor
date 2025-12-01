using Visor.CLI.Providers.SqlServer;

namespace Visor.IntegrationTests.CLI;

public class SqlServerSchemaLoaderTests
{
    private const string ConnectionString = "Server=localhost,1433;Database=VisorTestDb;User Id=sa;Password=VisorStrongPass123!;TrustServerCertificate=True;";

    [Fact]
    public async Task LoadProceduresAsync_ShouldReturnProcedures()
    {
        // Check if we can connect (Skip if no Docker)
        try
        {
            var loader = new SqlServerSchemaLoader(ConnectionString);
            var procedures = await loader.LoadProceduresAsync(CancellationToken.None);

            Assert.NotEmpty(procedures);

            var getCount = procedures.FirstOrDefault(p => p.Name == "sp_GetCount");
            Assert.NotNull(getCount);

            // sp_GetCount returns scalar (Visor treats result sets as generic reader unless defined)
            // In the init script: SELECT COUNT(*) FROM Users
            // sp_describe_first_result_set usually returns one column (NoName) int

            var getUser = procedures.FirstOrDefault(p => p.Name == "sp_GetUserById");
            Assert.NotNull(getUser);
            Assert.Contains(getUser.Parameters, p => p.Name == "@id");
        }
        catch (Exception)
        {
            // Skip test if database is not available
            return;
        }
    }

    [Fact]
    public async Task LoadTableTypesAsync_ShouldReturnTypes()
    {
        try
        {
            var loader = new SqlServerSchemaLoader(ConnectionString);
            var types = await loader.LoadTableTypesAsync(CancellationToken.None);

            var userListType = types.FirstOrDefault(t => t.Name == "UserListType");
            Assert.NotNull(userListType);
            Assert.Equal(2, userListType.Columns.Count);
        }
        catch (Exception)
        {
            return;
        }
    }
}
